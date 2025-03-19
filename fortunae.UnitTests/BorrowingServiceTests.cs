using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using fortunae.Service.Services;
using fortunae.Infrastructure.Interfaces;
using fortunae.Service.DTOs;
using fortunae.Domain.Entities;
using fortunae.Domain.Constants;


namespace fortunae.UnitTests;

public class BorrowingServiceTests
{
    private readonly Mock<IBorrowingRepository> _mockBorrowingRepository;
    private readonly Mock<IBookRepository> _mockBookRepository;
    private readonly Mock<IRatingRepository> _mockRatingRepository;
    private readonly BorrowingService _borrowingService;

    public BorrowingServiceTests()
    {
        _mockBorrowingRepository = new Mock<IBorrowingRepository>();
        _mockBookRepository = new Mock<IBookRepository>();
        _mockRatingRepository = new Mock<IRatingRepository>();

        _borrowingService = new BorrowingService(
            _mockBorrowingRepository.Object,
            _mockBookRepository.Object,
            _mockRatingRepository.Object
        );
    }

    [Fact]
    public async Task BorrowBookAsync_ShouldBorrowBook_WhenConditionsAreMet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            IsAvailable = true
        };

        var activeBorrowings = new List<Borrowing>();

        _mockBookRepository
            .Setup(repo => repo.GetBookByIdAsync(bookId))
            .ReturnsAsync(book);

        _mockBorrowingRepository
            .Setup(repo => repo.GetActiveBorrowingsByUserAsync(userId))
            .ReturnsAsync(activeBorrowings);

        _mockBorrowingRepository
            .Setup(repo => repo.AddBorrowingAsync(It.IsAny<Borrowing>()))
            .Returns(Task.CompletedTask);

        _mockBookRepository
            .Setup(repo => repo.UpdateBookAsync(It.IsAny<Book>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _borrowingService.BorrowBookAsync(userId, bookId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bookId, result.BookId);
        Assert.Equal(userId, result.UserId);
        Assert.False(book.IsAvailable); 

        
        _mockBorrowingRepository.Verify(repo => repo.AddBorrowingAsync(It.IsAny<Borrowing>()), Times.Once);

        
        _mockBookRepository.Verify(repo => repo.UpdateBookAsync(It.IsAny<Book>()), Times.Once);
    }

    [Fact]
    public async Task BorrowBookAsync_ShouldThrowException_WhenBookDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        _mockBookRepository
            .Setup(repo => repo.GetBookByIdAsync(bookId))
            .ReturnsAsync((Book)null); 

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _borrowingService.BorrowBookAsync(userId, bookId)
        );

        Assert.Equal(Message.BookDoesNotExist, exception.Message);
    }

    [Fact]
    public async Task BorrowBookAsync_ShouldThrowException_WhenBookIsUnavailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            IsAvailable = false
        };

        _mockBookRepository
            .Setup(repo => repo.GetBookByIdAsync(bookId))
            .ReturnsAsync(book);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _borrowingService.BorrowBookAsync(userId, bookId)
        );

        Assert.Equal(Message.BookNotAvailable, exception.Message);
    }

    [Fact]
    public async Task BorrowBookAsync_ShouldThrowException_WhenUserHasMaxBorrowings()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            IsAvailable = true
        };

        var activeBorrowings = new List<Borrowing>
        {
            new Borrowing(), new Borrowing(), new Borrowing()
        };

        _mockBookRepository
            .Setup(repo => repo.GetBookByIdAsync(bookId))
            .ReturnsAsync(book);

        _mockBorrowingRepository
            .Setup(repo => repo.GetActiveBorrowingsByUserAsync(userId))
            .ReturnsAsync(activeBorrowings);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _borrowingService.BorrowBookAsync(userId, bookId)
        );

        Assert.Equal(Message.BorrowLimit, exception.Message);
    }
}
