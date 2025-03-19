﻿
namespace fortunae.Service.Services
{
    using fortunae.Service.DTOs;
    using fortunae.Service.Interfaces;
    using fortunae.Domain.Entities;
    using fortunae.Infrastructure.Interfaces;
    using Microsoft.Extensions.Caching.Distributed;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Caching.Memory;
    using fortunae.Service.Services.CacheService;
    using Microsoft.EntityFrameworkCore;
    using System.Diagnostics;
    using static fortunae.Service.DTOs.ResponseMessages;
    using fortunae.Domain.Constants;
    using Amazon.S3.Model;

    public class BookService : IBookService
    {
        private readonly IBookRepository _bookRepository;
        private readonly IRatingRepository _ratingRepository;
        private readonly ILogger<BookService> _logger;
        private readonly IImageService _imageService;
        private readonly IRedisService _cache;
        private const int CACHE_DURATION_MINUTES = 10;

        public BookService(IBookRepository bookRepository, ILogger<BookService> logger, IImageService imageService, IRedisService cache, IRatingRepository ratingRepository)
        {
            _bookRepository = bookRepository;
            _logger = logger;
            _imageService = imageService;
            _ratingRepository = ratingRepository;
            _cache = cache;
        }

        public async Task<ApiSuccessResponse<BookDTO>> AddBookAsync(CreateBookDTO createBookDto)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (createBookDto.Image == null)
                {
                    return new ApiSuccessResponse<BookDTO>
                    {
                        Status = 400,
                        Message = Message.ImageCannotBeEmpty,
                        Data = null,
                        RuntimeSeconds = stopwatch.Elapsed.TotalSeconds,
                        Timestamp = DateTime.UtcNow
                    };
                }

                ImageUrlResponseDto imageResponse = await _imageService.UploadImageAsync(createBookDto.Image);
                var book = new Book
                {
                    Id = Guid.NewGuid(),
                    Title = createBookDto.Title,
                    Author = createBookDto.Author,
                    Genre = createBookDto.Genre,
                    ISBN = createBookDto.ISBN,
                    Description = createBookDto.Description,
                    IsAvailable = true,
                    BookImage = imageResponse.PresignedUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _bookRepository.AddBookAsync(book);
                await InvalidateBookCaches(book.Id);
                stopwatch.Stop();

                return new ApiSuccessResponse<BookDTO>
                {
                    Status = 201,
                    Message = Message.BookAdded,
                    Data = MapToBookDTO(book),
                    RuntimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while adding the book: {Exception}", ex);
                return new ApiSuccessResponse<BookDTO>
                {
                    Status = 500,
                    Message = Message.ErrorAddingBook,
                    Data = null,
                    RuntimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Timestamp = DateTime.UtcNow
                };
            }
        }


        public async Task<ResponseMessages.ApiSuccessResponse<BookDTO>> UpdateBookAsync(Guid id, UpdateBookDTO updateBookDto)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var book = await _bookRepository.GetBookByIdAsync(id);
                if (book == null)
                {
                    return new ResponseMessages.ApiSuccessResponse<BookDTO>
                    {
                        Status = 404,
                        Message = "Book not found.",
                        RuntimeSeconds = stopwatch.Elapsed.TotalSeconds
                    };
                }

                if (updateBookDto.Image != null)
                {
                    ImageUrlResponseDto imageResponse = await _imageService.UploadImageAsync(updateBookDto.Image);
                    book.BookImage = imageResponse.PresignedUrl;
                }

                book.Title = updateBookDto.Title ?? book.Title;
                book.Author = updateBookDto.Author ?? book.Author;
                book.Genre = updateBookDto.Genre ?? book.Genre;
                book.Description = updateBookDto.Description ?? book.Description;
                book.ISBN = updateBookDto.ISBN ?? book.ISBN;
                book.IsAvailable = updateBookDto.IsAvailable ?? book.IsAvailable;
                book.UpdatedAt = DateTime.UtcNow;

                await _bookRepository.UpdateBookAsync(book);
                await InvalidateBookCaches(book.Id);
                stopwatch.Stop();

                return ResponseMessages.ApiSuccessResponse<BookDTO>.Create(MapToBookDTO(book), stopwatch);
            }
            catch (Exception ex)
            {
                return new ResponseMessages.ApiSuccessResponse<BookDTO>
                {
                    Status = 500,
                    Message = Message.ErrorUpdatingBook,
                    RuntimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Data = null,
                    
                };
            }
        }

        public async Task<ResponseMessages.ApiSuccessResponse<BookDTO>> GetBooksByIdAsync(Guid bookId)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string cacheKey = $"Book_{bookId}";

                var cachedBook = await _cache.GetAsync<BookDTO>(cacheKey);
                if (cachedBook != null)
                {
                    stopwatch.Stop();
                    return ResponseMessages.ApiSuccessResponse<BookDTO>.Create(
                        cachedBook, stopwatch
                    );
                }

                var book = await _bookRepository.GetBookByIdAsync(bookId);
                if (book == null)
                {
                    stopwatch.Stop();
                    return new ResponseMessages.ApiSuccessResponse<BookDTO>
                    {
                        Status = 404,
                        Message = $"Book with ID {bookId} not found.",
                        Data = null,
                        RuntimeSeconds = stopwatch.Elapsed.TotalSeconds
                    };
                }

                var bookDto = MapToBookDTO(book);
                await _cache.SetAsync(cacheKey, bookDto, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                stopwatch.Stop();
                return ResponseMessages.ApiSuccessResponse<BookDTO>.Create(
                    bookDto, stopwatch
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ResponseMessages.ApiSuccessResponse<BookDTO>
                {
                    Status = 500,
                    Message = Message.ErrorRetrievingBook,
                    Data = null,
                    RuntimeSeconds = stopwatch.Elapsed.TotalSeconds
                };
            }
        }


        public async Task<ResponseMessages.ApiSuccessResponse<PaginatedList<BookDTO>>> GetAllBooksAsync(bool includeUnavailable = false, int pageNumber = 1, int pageSize = 10)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string cacheKey = $"AllBooks_Page{pageNumber}_Size{pageSize}_Include{includeUnavailable}";

                var cachedBooks = await _cache.GetAsync<List<BookDTO>>(cacheKey);
                if (cachedBooks != null)
                {
                    stopwatch.Stop();
                    return ResponseMessages.ApiSuccessResponse<PaginatedList<BookDTO>>.Create(
                        new PaginatedList<BookDTO>(
                            includeUnavailable ? cachedBooks : cachedBooks.Where(b => b.IsAvailable).ToList(),
                            pageNumber, pageSize, cachedBooks.Count
                        ),
                        stopwatch, pageNumber, pageSize, cachedBooks.Count
                    );
                }

                IQueryable<Book> query = _bookRepository.GetBooksAsync(null, null);
                if (!includeUnavailable)
                {
                    query = query.Where(b => b.IsAvailable);
                }

                var paginatedBooks = await PaginatedList<Book>.CreateAsync(query, pageNumber, pageSize);
                var bookDtos = paginatedBooks.Select(MapToBookDTO).ToList();

                await _cache.SetAsync(cacheKey, bookDtos, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                stopwatch.Stop();
                return ResponseMessages.ApiSuccessResponse<PaginatedList<BookDTO>>.Create(
                    new PaginatedList<BookDTO>(bookDtos, pageNumber, pageSize, paginatedBooks.TotalCount),
                    stopwatch, pageNumber, pageSize, paginatedBooks.TotalCount
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ResponseMessages.ApiSuccessResponse<PaginatedList<BookDTO>>.Create(
                    null, 
                    stopwatch, pageNumber, pageSize, 0 
                );
            }

            }

        public async Task<PaginatedList<BookDTO>> GetAvailableBooksAsync(string? filter = null, int pageNumber = 1, int pageSize = 10)
        {
            string cacheVersion = await GetCacheVersionAsync();
            string cacheKey = $"AvailableBooks_{cacheVersion}_Page{pageNumber}_Size{pageSize}_Filter{filter}";

            var cachedBooks = await _cache.GetAsync<List<BookDTO>>(cacheKey);
            if (cachedBooks != null)
            {
                var filteredBooks = string.IsNullOrWhiteSpace(filter)
                    ? cachedBooks
                    : cachedBooks.Where(b =>
                          b.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                          b.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                          b.Genre.Contains(filter, StringComparison.OrdinalIgnoreCase))
                      .ToList();

                // Sort cached results in descending order
                var sortedBooks = filteredBooks.OrderByDescending(b => b.CreatedAt).ToList();

                return new PaginatedList<BookDTO>(sortedBooks, cachedBooks.Count, pageNumber, pageSize);
            }

            
            IQueryable<Book> query = _bookRepository.GetBooksAsync(null, null).Where(b => b.IsAvailable);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(b => EF.Functions.Like(b.Title, $"%{filter}%") ||
                                         EF.Functions.Like(b.Author, $"%{filter}%") ||
                                         EF.Functions.Like(b.Genre, $"%{filter}%"));
            }

            
            query = query.OrderByDescending(b => b.CreatedAt);

            var paginatedBooks = await PaginatedList<Book>.CreateAsync(query, pageNumber, pageSize);
            var bookDtos = paginatedBooks.Select(MapToBookDTO).ToList();

            await _cache.SetAsync(cacheKey, bookDtos, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            return new PaginatedList<BookDTO>(bookDtos, paginatedBooks.TotalCount, pageNumber, pageSize);
        }
        public async Task AddRatingAsync(Guid bookId, Guid userId, int value, string? comment = null)
        {
            var rating = new Rating
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                UserId = userId,
                Value = value,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            await _ratingRepository.AddRatingAsync(rating);

            var ratings = await _ratingRepository.GetRatingsByBookIdAsync(bookId);
            var book = await _bookRepository.GetBookByIdAsync(bookId);
            Console.WriteLine($"Ratings: {ratings.Count()}"); 
            if (ratings.Any())
            {
                book.AverageRating = (decimal)ratings.Average(r => r.Value);
            }
            else
            {
                book.AverageRating = 0;  
            }

            
            book.AverageRating = (decimal)ratings.Average(r => r.Value);

            await _bookRepository.UpdateBookAsync(book);
        }

        public async Task<List<BookDTO>> GetTopRatedBooksAsync(int top = 10)
        {
            var books =  _bookRepository.GetBooksAsync(null, null);
            return books
                .OrderByDescending(b => b.AverageRating)
                .Take(top)
                .Select(MapToBookDTO)
                .ToList();
        }
        public async Task<List<BookDTO>> GetCachedTopRatedBooksAsync()
        {
            string cacheVersion = await GetCacheVersionAsync();
            string cacheKey = "TopRatedBooks";

            var cachedBooks = await _cache.GetAsync<List<BookDTO>>(cacheKey);
            if (cachedBooks != null)
                return cachedBooks;

            var books = await GetTopRatedBooksAsync();
            await _cache.SetAsync(cacheKey, books, TimeSpan.FromMinutes(15));

            return books;
        }

        public async Task<PaginatedList<BookDTO>> SearchBooksAsync(string? title = null, string? author = null, string? genre = null,bool? isAvailable = null, int pageNumber = 1, int pageSize = 10)
        {
            IQueryable<Book> query = _bookRepository.GetBooksAsync(null, null);

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(b => EF.Functions.Like(b.Title, $"%{title}%"));
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                query = query.Where(b => EF.Functions.Like(b.Author, $"%{author}%"));
            }

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(b => EF.Functions.Like(b.Genre, $"%{genre}%"));
            }

            if (isAvailable.HasValue)
            {
                query = query.Where(b => b.IsAvailable == isAvailable.Value);
            }

            var paginatedBooks = await PaginatedList<Book>.CreateAsync(query, pageNumber, pageSize);

            return new PaginatedList<BookDTO>(
                paginatedBooks.Select(MapToBookDTO).ToList(),
                paginatedBooks.TotalCount,
                paginatedBooks.CurrentPage,
                paginatedBooks.PageSize);
        }

        public async Task<List<BookDTO>> GetRelatedBooksAsync(Guid bookId)
        {
            var book = await _bookRepository.GetBookByIdAsync(bookId);
            if (book == null)
                throw new KeyNotFoundException(Message.BookNotFound);

            var relatedBooks =  _bookRepository.GetBooksAsync(book.Genre, book.Author);
            return relatedBooks.Where(b => b.Id != bookId).Select(MapToBookDTO).ToList();
        }
        public async Task<List<RatingDTO>> GetRatingsByBookIdAsync(Guid bookId)
        {
            var ratings = await _ratingRepository.GetRatingsByBookIdAsync(bookId);
            return ratings.Select(r => new RatingDTO
            {
                Id = r.Id,
                UserId = r.UserId,
                Value = r.Value,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }).ToList();
        }
        public async Task<List<RatingDTO>> GetRatingsByUserIdAsync(Guid userId)
        {
            var ratings = await _ratingRepository.GetRatingsByUserIdAsync(userId);
            return ratings.Select(r => new RatingDTO
            {
                Id = r.Id,
                BookId = r.BookId,
                Value = r.Value,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<bool> DeleteBookAsync(Guid bookId)
        {
            var book = await _bookRepository.GetBookByIdAsync(bookId);
            if (book == null)
            {
                _logger.LogWarning($"Book with ID {bookId} not found.");
                return false;
            }

            await _bookRepository.DeleteBookAsync(book);
            await InvalidateBookCaches(bookId);

            _logger.LogInformation($"Book with ID {bookId} deleted and cache invalidated.");
            return true;
        }

        private async Task InvalidateBookCaches(Guid bookId)
        {
            string versionKey = "BookCacheVersion";
            await _cache.SetAsync(versionKey, Guid.NewGuid().ToString(), TimeSpan.FromDays(1));

            var keysToInvalidate = await _cache.GetKeysWithPrefixAsync("AvailableBooks_");

            var cacheKeys = new HashSet<string>
            {
                Message.cachekeyAllBooks,
                Message.cachekeyAvailableBooks,
                $"Book_{bookId}"
            };

            foreach (var key in keysToInvalidate)
            {
                cacheKeys.Add(key);
            }

            foreach (var key in cacheKeys)
            {
                await _cache.RemoveAsync(key);
            }
        }

        private async Task<string> GetCacheVersionAsync()
        {
            string versionKey = "BookCacheVersion";
            var version = await _cache.GetAsync<string>(versionKey);

            if (version == null)
            {
                version = Guid.NewGuid().ToString();
                await _cache.SetAsync(versionKey, version, TimeSpan.FromDays(1));
            }

            return version;
        }



        private BookDTO MapToBookDTO(Book book)
        {
            return new BookDTO
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author!,
                Genre = book.Genre!,
                Description = book.Description!,
                ISBN = book.ISBN!,
                IsAvailable = book.IsAvailable,
                BookImage = book.BookImage!,
                AverageRating = book.AverageRating,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };
        }
    }

}
