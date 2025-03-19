
using fortunae.Domain.Entities;

namespace fortunae.Infrastructure.Interfaces
{
    public interface IRatingRepository
    {
        Task AddRatingAsync(Rating rating);
        Task<List<Rating>> GetRatingsByBookIdAsync(Guid bookId);
        Task<List<Rating>> GetRatingsByUserIdAsync(Guid userId);
    }

}
