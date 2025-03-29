

// using fortunae.Domain.Entities;

// namespace fortunae.Infrastructure.Interfaces
// {
//     public interface IUserRepository
//     {
//         Task<User> GetUserByUsernameAsync(string username);
//         Task AddUserAsync(User user);
//         Task<User> GetUserByIdAsync(Guid id);
//         Task DeleteUserAsync(User user);
//         Task<User> GetUserByEmailAsync(string email);
//         Task UpdateUserAsync(User user);
//     }
// }


// fortunae.Infrastructure/Interfaces/IUserRepository.cs
using fortunae.Domain.Entities;

namespace fortunae.Infrastructure.Interfaces
{
    public interface IUserRepository
    {
        Task<User> GetUserByUsernameAsync(string username);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByIdAsync(Guid id);
        Task AddUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task DeleteUserAsync(User user);
    }
}