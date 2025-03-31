
namespace fortunae.Service.Interfaces
{
    using fortunae.Domain.Entities;
    using fortunae.Service.DTOs;
    using static fortunae.Service.DTOs.ResponseMessages;
    using ResponseMessages = fortunae.Service.DTOs.ResponseMessages;

    public interface IBookService
    {
        Task<ApiSuccessResponse<BookDTO>> AddBookAsync(CreateBookDTO createBookDto);
        Task<ApiSuccessResponse<BookDTO>> UpdateBookAsync(Guid id, UpdateBookDTO updateBookDto);
        Task<ApiSuccessResponse<BookDTO>> GetBooksByIdAsync(Guid bookId);
        Task<ApiSuccessResponse<PaginatedList<BookDTO>>> GetAllBooksAsync(bool includeUnavailable = false, int pageNumber = 1, int pageSize = 10);
        Task<PaginatedList<BookDTO>> GetAvailableBooksAsync(string? filter = null, int pageNumber = 1, int pageSize = 10);
        Task AddRatingAsync(Guid bookId, Guid userId, int value, string? comment = null);
        Task<List<BookDTO>> GetTopRatedBooksAsync(int top = 10);
        // Task<List<BookDTO>> GetCachedTopRatedBooksAsync();
        Task<PaginatedList<BookDTO>> SearchBooksAsync(string? title = null, string? author = null, string? genre = null, bool? isAvailable = null, int pageNumber = 1, int pageSize = 10);
        Task<List<BookDTO>> GetRelatedBooksAsync(Guid bookId);
        Task<List<RatingDTO>> GetRatingsByBookIdAsync(Guid bookId);
        Task<List<RatingDTO>> GetRatingsByUserIdAsync(Guid userId);
        Task<bool> DeleteBookAsync(Guid bookId);
    }
}