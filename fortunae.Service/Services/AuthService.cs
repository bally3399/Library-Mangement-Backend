using fortunae.Service.DTO;
using fortunae.Service.DTOs;
using fortunae.Service.Interfaces;
using Microsoft.Extensions.Configuration;
using fortunae.Domain.Constants;
using fortunae.Infrastructure.Interfaces;
using fortunae.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;


namespace fortunae.Service.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;

        public AuthService(IConfiguration configuration, IUserRepository userRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
        }

        public async Task<bool> RegisterAsync(RegisterDTO registerDto)
        {
            var existingUser = await _userRepository.GetUserByUsernameAsync(registerDto.Username);
            if (existingUser != null)
                throw new InvalidOperationException(Message.UserNameAlreadyExists);

            var existingEmail = await _userRepository.GetUserByEmailAsync(registerDto.Email);
            if (existingEmail != null)
                throw new InvalidOperationException(Message.EmailAlreadyExists);

            var passwordHash = HashPassword(registerDto.Password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                Role = registerDto.Role,
                Name = registerDto.Name,
                 DateOfBirth = registerDto.DateOfBirth.Kind == DateTimeKind.Utc 
            ? registerDto.DateOfBirth 
            : DateTime.SpecifyKind(registerDto.DateOfBirth, DateTimeKind.Utc),
                ProfileSummary = registerDto.ProfileSummary
            };

            await _userRepository.AddUserAsync(user);
            return true;
        }

        public async Task<string> LoginAsync(string identifier, string password)
        {
            var user = await _userRepository.GetUserByUsernameAsync(identifier) ??
                       await _userRepository.GetUserByEmailAsync(identifier);

            if (user == null || !VerifyPassword(password, user.PasswordHash))
                throw new UnauthorizedAccessException(Message.InvalidCredentials);

            return GenerateJwtToken(user.Username, user.Role, user.Id.ToString());
        }

        public async Task<bool> ResetPasswordAsync(string email, string newPassword)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
                throw new KeyNotFoundException(Message.UserNotFound);

            user.PasswordHash = HashPassword(newPassword);
            await _userRepository.UpdateUserAsync(user);
            return true;
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDTO profileDto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException(Message.UserNotFound);

            user.Name = profileDto.Name ?? user.Name;
            user.DateOfBirth = profileDto.DateOfBirth ?? user.DateOfBirth;
            user.ProfileSummary = profileDto.ProfileSummary ?? user.ProfileSummary;

            await _userRepository.UpdateUserAsync(user);
            return true;
        }

        public async Task<User> GetUserByIdAsync(Guid id)
        {
            return await _userRepository.GetUserByIdAsync(id);
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
                throw new KeyNotFoundException(Message.UserNotFound);

            await _userRepository.DeleteUserAsync(user);
            return true;
        }

        




private string GenerateJwtToken(string username, string role, string userId)
{
    // Fetch environment variables with fallback to configuration
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? _configuration["Jwt:Key"];
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["Jwt:Issuer"];
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["Jwt:Audience"];
    var jwtExpirationMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRATION") ?? _configuration["Jwt:Expiration"], out int minutes) ? minutes : 30;

    // Validate inputs and log issues
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
        throw new ArgumentException($"JWT_SECRET_KEY is missing or too short. Must be at least 32 characters. Current value: '{jwtKey}'");
    if (string.IsNullOrEmpty(jwtIssuer))
        throw new ArgumentException($"JWT_ISSUER is missing. Current value: '{jwtIssuer}'");
    if (string.IsNullOrEmpty(jwtAudience))
        throw new ArgumentException($"JWT_AUDIENCE is missing. Current value: '{jwtAudience}'");

    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, role),
        new Claim("UserId", userId)
    };

    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(jwtExpirationMinutes),
        signingCredentials: credentials
    );

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenString = tokenHandler.WriteToken(token);

    // Log the token for debugging
    Console.WriteLine($"Generated JWT: {tokenString}");

    return tokenString;
}
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            var passwordHash = HashPassword(password);
            return passwordHash == hashedPassword;
        }
    }

}
