using fortunae.Service.DTO;
using fortunae.Service.DTOs;
using fortunae.Domain.Entities;


namespace fortunae.Service.Interfaces
{
    public interface IAuthService
    {
        Task<string> LoginAsync(string username, string password);
        Task<bool> RegisterAsync(RegisterDTO registerDto);
        Task<bool> DeleteUserAsync(Guid id);
        Task<User> GetUserByIdAsync(Guid id);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDTO profileDto);
        Task<bool> ResetPasswordAsync(string email, string newPassword);
    }
}
