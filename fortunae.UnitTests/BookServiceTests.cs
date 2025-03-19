using Xunit;
using Moq;
using fortunae.Service.Services;
using fortunae.Service.Interfaces;
using fortunae.Service.DTOs;
using fortunae.Domain.Entities;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using fortunae.Infrastructure.Interfaces;
using fortunae.Service.Services.CacheService;
using Moq.EntityFrameworkCore;
using MockQueryable.Moq;
using Microsoft.AspNetCore.Http;
using MockQueryable;

public class BookServiceTests
{
    private readonly Mock<IBookRepository> _bookRepositoryMock;
    private readonly Mock<IRatingRepository> _ratingRepositoryMock;
    private readonly Mock<IImageService> _imageServiceMock;
    private readonly Mock<IRedisService> _cacheMock;
    private readonly Mock<ILogger<BookService>> _loggerMock;
    private readonly BookService _bookService;

    public BookServiceTests()
    {
        _bookRepositoryMock = new Mock<IBookRepository>();
        _ratingRepositoryMock = new Mock<IRatingRepository>();
        _imageServiceMock = new Mock<IImageService>();
        _cacheMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<BookService>>();

        _bookService = new BookService(
            _bookRepositoryMock.Object,
            _loggerMock.Object,
            _imageServiceMock.Object,
            _cacheMock.Object,
            _ratingRepositoryMock.Object
        );
    }

    
    [Fact]
    public async Task AddBookAsync_ShouldReturnSuccessResponse_WhenValidInputProvided()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var stream = new MemoryStream(new byte[0]);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.Length).Returns(0);
        fileMock.Setup(f => f.FileName).Returns("test.jpg");

        var createBookDto = new CreateBookDTO
        {
            Title = "Test Book",
            Author = "Test Author",
            Genre = "Fiction",
            ISBN = "1234567890",
            Description = "A test book description",
            Image = fileMock.Object,
        };

        var imageResponse = new ImageUrlResponseDto { PresignedUrl = "http://image.url" };
        _imageServiceMock.Setup(x => x.UploadImageAsync(fileMock.Object)).ReturnsAsync(imageResponse);
        _bookRepositoryMock.Setup(x => x.AddBookAsync(It.IsAny<Book>())).Returns(Task.CompletedTask);

        // Act
        var result = await _bookService.AddBookAsync(createBookDto);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(201);
        result.Data.Title.Should().Be(createBookDto.Title);
        result.Data.BookImage.Should().Be(imageResponse.PresignedUrl);
        result.RuntimeSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    // ? Test: Get Book by ID (Book Exists)
    [Fact]
    public async Task GetBooksByIdAsync_ShouldReturnBook_WhenBookExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Id = bookId,
            Title = "Sample Book",
            Author = "Author",
            Genre = "Genre",
            ISBN = "123456",
            Description = "Description",
            IsAvailable = true
        };

        _bookRepositoryMock.Setup(x => x.GetBookByIdAsync(bookId)).ReturnsAsync(book);

        // Act
        ResponseMessages.ApiSuccessResponse<BookDTO> result = await _bookService.GetBooksByIdAsync(bookId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(200);
        result.Data.Id.Should().Be(bookId);
        result.RuntimeSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    // ? Test: Get Book by ID (Book Does Not Exist)
    [Fact]
    public async Task GetBooksByIdAsync_ShouldReturnError_WhenBookDoesNotExist()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        _bookRepositoryMock.Setup(x => x.GetBookByIdAsync(bookId)).ReturnsAsync((Book)null);

        // Act
        var result = await _bookService.GetBooksByIdAsync(bookId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(404);
       // result.Message.Should().Be("Book not found.");
    }

    // ? Test: Delete Book (Book Does Not Exist)
    [Fact]
    public async Task DeleteBookAsync_ShouldReturnFalse_WhenBookDoesNotExist()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        _bookRepositoryMock.Setup(x => x.GetBookByIdAsync(bookId)).ReturnsAsync((Book)null);

        // Act
        var result = await _bookService.DeleteBookAsync(bookId);

        // Assert
        result.Should().BeFalse();
    }

    // ? Test: Delete Book (Book Exists)
    [Fact]
    public async Task DeleteBookAsync_ShouldReturnTrue_WhenBookExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Sample Book" };
        _bookRepositoryMock.Setup(x => x.GetBookByIdAsync(bookId)).ReturnsAsync(book);
        _bookRepositoryMock.Setup(x => x.DeleteBookAsync(book)).Returns(Task.CompletedTask);

        // Act
        var result = await _bookService.DeleteBookAsync(bookId);

        // Assert
        result.Should().BeTrue();
    }

    // ? Test: Get Paginated Books
    [Fact]
    public async Task GetAllBooksAsync_ShouldReturnPaginatedBooks()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Id = Guid.NewGuid(), Title = "Book 1", IsAvailable = true },
            new Book { Id = Guid.NewGuid(), Title = "Book 2", IsAvailable = true }
        };

        var mockBooks = books.AsQueryable().BuildMock();

        _bookRepositoryMock.Setup(x => x.GetBooksAsync(null, null)).Returns(mockBooks);


        // Act
        var result = await _bookService.GetAllBooksAsync();

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(200);
        result.Data.Should().HaveCount(2);
        result.Pagination.Should().NotBeNull();
        result.Pagination.PageNumber.Should().Be(1);
        result.Pagination.PageSize.Should().Be(10);
        result.Pagination.TotalCount.Should().Be(2);
    }

    
}

