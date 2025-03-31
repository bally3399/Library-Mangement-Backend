// using fortunae.Service.DTO;
// using fortunae.Service.DTOs;
// using fortunae.Service.Interfaces;
// using Microsoft.Extensions.Configuration;
// using fortunae.Domain.Constants;
// using fortunae.Infrastructure.Interfaces;
// using fortunae.Domain.Entities;
// //using Microsoft.IdentityModel.JsonWebTokens;
// //using System.IdentityModel.Tokens.Jwt;
// //using System.Security.Claims;
// using System.Text;
// using System.Security.Cryptography;
// using System.Text.Json;


// namespace fortunae.Service.Services
// {
//     public class AuthService : IAuthService
//     {
//         private readonly IConfiguration _configuration;
//         private readonly IUserRepository _userRepository;
//         private readonly CustomTokenService _tokenService;

//         private readonly string _secretKey;

//         public AuthService(IConfiguration configuration)
//         {
//             _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? configuration["Jwt:Key"];
//             if (string.IsNullOrEmpty(_secretKey))
//                 throw new ArgumentException("Secret key is missing.");
//         }

//         public async Task<bool> RegisterAsync(RegisterDTO registerDto)
//         {
//             var existingUser = await _userRepository.GetUserByUsernameAsync(registerDto.Username);
//             if (existingUser != null)
//                 throw new InvalidOperationException(Message.UserNameAlreadyExists);

//             var existingEmail = await _userRepository.GetUserByEmailAsync(registerDto.Email);
//             if (existingEmail != null)
//                 throw new InvalidOperationException(Message.EmailAlreadyExists);

//             var passwordHash = HashPassword(registerDto.Password);

//             var user = new User
//             {
//                 Id = Guid.NewGuid(),
//                 Username = registerDto.Username,
//                 Email = registerDto.Email,
//                 PasswordHash = passwordHash,
//                 Role = registerDto.Role,
//                 Name = registerDto.Name,
//                  DateOfBirth = registerDto.DateOfBirth.Kind == DateTimeKind.Utc 
//             ? registerDto.DateOfBirth 
//             : DateTime.SpecifyKind(registerDto.DateOfBirth, DateTimeKind.Utc),
//                 ProfileSummary = registerDto.ProfileSummary
//             };

//             await _userRepository.AddUserAsync(user);
//             return true;
//         }

//         public async Task<string> LoginAsync(string identifier, string password)
//         {
//             var user = await _userRepository.GetUserByUsernameAsync(identifier) ??
//                        await _userRepository.GetUserByEmailAsync(identifier);

//             if (user == null || !VerifyPassword(password, user.PasswordHash))
//                 throw new UnauthorizedAccessException("Invalid credentials.");

//             return GenerateToken(user);
//         }

//         public string GenerateToken(User user)
//         {
//             // Header
//             var header = new Dictionary<string, string>
//             {
//                 { "alg", "HS256" },
//                 { "typ", "JWT" }
//             };
//             var headerJson = JsonSerializer.Serialize(header);
//             var headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson))
//                 .TrimEnd('='); // Remove padding for cleaner JWT

//             // Payload
//             var payload = new Dictionary<string, object>
//             {
//                 { "sub", user.Username },
//                 { "role", user.Role },
//                 { "userId", user.Id.ToString() },
//                 { "exp", DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds() }, // Matches your ~60-day expiration
//                 { "iss", "me" },
//                 { "aud", "you" }
//             };
//             var payloadJson = JsonSerializer.Serialize(payload);
//             var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
//                 .TrimEnd('=');

//             // Signature
//             using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
//             var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}")))
//                 .TrimEnd('=');

//             return $"{headerBase64}.{payloadBase64}.{signature}";
//         }

//         public async Task<bool> ResetPasswordAsync(string email, string newPassword)
//         {
//             var user = await _userRepository.GetUserByEmailAsync(email);
//             if (user == null)
//                 throw new KeyNotFoundException(Message.UserNotFound);

//             user.PasswordHash = HashPassword(newPassword);
//             await _userRepository.UpdateUserAsync(user);
//             return true;
//         }

//         public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDTO profileDto)
//         {
//             var user = await _userRepository.GetUserByIdAsync(userId);
//             if (user == null)
//                 throw new KeyNotFoundException(Message.UserNotFound);

//             user.Name = profileDto.Name ?? user.Name;
//             user.DateOfBirth = profileDto.DateOfBirth ?? user.DateOfBirth;
//             user.ProfileSummary = profileDto.ProfileSummary ?? user.ProfileSummary;

//             await _userRepository.UpdateUserAsync(user);
//             return true;
//         }

//         public async Task<User> GetUserByIdAsync(Guid id)
//         {
//             return await _userRepository.GetUserByIdAsync(id);
//         }

//         public async Task<bool> DeleteUserAsync(Guid id)
//         {
//             var user = await _userRepository.GetUserByIdAsync(id);
//             if (user == null)
//                 throw new KeyNotFoundException(Message.UserNotFound);

//             await _userRepository.DeleteUserAsync(user);
//             return true;
//         }





//         private string HashPassword(string password)
//         {
//             using var sha256 = SHA256.Create();
//             var bytes = Encoding.UTF8.GetBytes(password);
//             var hash = sha256.ComputeHash(bytes);
//             return Convert.ToBase64String(hash);
//         }

//         private bool VerifyPassword(string password, string hashedPassword)
//         {
//             var passwordHash = HashPassword(password);
//             return passwordHash == hashedPassword;
//         }

//     }

// }


// fortunae.Service/Services/AuthService.cs
using fortunae.Service.DTOs;


using fortunae.Service.Interfaces;
using Microsoft.Extensions.Configuration;
using fortunae.Domain.Constants;
using fortunae.Infrastructure.Interfaces;
using fortunae.Domain.Entities;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using fortunae.Service.DTO;
using Microsoft.Extensions.Logging;
namespace fortunae.Service.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;
        private readonly CustomTokenService _tokenService; // Note: This might not be needed if GenerateToken is here
        private readonly string _secretKey;

        private readonly ILogger<AuthService> _logger;

        // Updated constructor
        public AuthService(IConfiguration configuration, IUserRepository userRepository, ILogger<AuthService> logger, CustomTokenService tokenService = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tokenService = tokenService; // Optional, remove if not used
            _logger = logger;
            _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(_secretKey))
                throw new ArgumentException("Secret key is missing.");
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
                throw new UnauthorizedAccessException("Invalid credentials.");

            return GenerateToken(user);
        }

        public string GenerateToken(User user)
{
    // Header
    var header = new Dictionary<string, string> { { "alg", "HS256" }, { "typ", "JWT" } };
    var headerJson = JsonSerializer.Serialize(header);
    var headerBase64Url = Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(headerJson)); // 👈 Use helper

    // Payload
    var payload = new Dictionary<string, object>
    {
        { "sub", user.Username ?? throw new ArgumentNullException(nameof(user.Username)) },
        { "role", user.Role ?? "user" },
        { "userId", user.Id.ToString() },
        { "exp", DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds() },
        { "iss", "me" },
        { "aud", "you" }
    };
    var payloadJson = JsonSerializer.Serialize(payload);
    var payloadBase64Url = Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(payloadJson)); // 👈 Use helper

// In AuthService.GenerateToken():
Console.WriteLine($"SECRET KEY (GENERATION): {_secretKey}");

// In CustomTokenAuthenticationHandler.ValidateToken():
 _logger.LogInformation($"SECRET KEY (VALIDATION): {_secretKey}");
    // Signature
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
    byte[] signatureBytes = hmac.ComputeHash(
        Encoding.UTF8.GetBytes($"{headerBase64Url}.{payloadBase64Url}")
    );
string signature = Base64UrlHelper.Encode(signatureBytes); // 👈 Use helper
string headerPayload = $"{headerBase64Url}.{payloadBase64Url}";
Console.WriteLine($"Data being signed: {headerPayload}");
Console.WriteLine($"Generated signature: {signature}");
    return $"{headerBase64Url}.{payloadBase64Url}.{signature}";
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
            user.DateOfBirth = DateTime.SpecifyKind((DateTime)(profileDto.DateOfBirth ?? user.DateOfBirth), DateTimeKind.Utc);
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

// Add this class to a shared namespace (e.g., fortunae.Service.Utilities)
public static class Base64UrlHelper
{
    public static string Encode(byte[] input)
    {
        string base64 = Convert.ToBase64String(input);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static byte[] Decode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}


    }
}