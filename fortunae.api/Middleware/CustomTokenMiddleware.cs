using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace fortunae.Service.Middleware
{
    public class CustomTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _secretKey;
        private readonly ILogger<CustomTokenMiddleware> _logger; // Added logger field

        public CustomTokenMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<CustomTokenMiddleware> logger) // Inject ILogger
        {
            _next = next;
            _logger = logger;
            _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(_secretKey))
                throw new ArgumentException("Secret key is missing.");
        }

        public async Task InvokeAsync(HttpContext context)
        {
                var path = context.Request.Path.Value?.ToLower();
    if (path != null && 
        (path.StartsWith("/api/auth/register") || path.StartsWith("/api/auth/login")))
    {
        await _next(context);
        return;
    }

    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
        !authHeader.ToString().StartsWith("Bearer "))
    {
        Console.WriteLine($"Missing or invalid Authorization header for path: {path}");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Authorization header missing or invalid.");
        return;
    }

    var authHeaderValue = authHeader.ToString();
    var token = authHeaderValue.Substring("Bearer ".Length).Trim();
    Console.WriteLine($"Raw Authorization Header: {authHeaderValue}"); // Debug
    Console.WriteLine($"Extracted Token: {token}"); // Debug
    Console.WriteLine($"Token Length: {token.Length}, Parts Count: {token.Split('.').Length}"); // Debug
    Console.WriteLine($"Validating token: {token} for path: {path}");

    if (!ValidateToken(token, out var payload))
    {
        Console.WriteLine($"Token validation failed for path: {path}");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid or expired token.");
        return;
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, payload.GetProperty("sub").GetString()),
        new Claim(ClaimTypes.Role, payload.GetProperty("role").GetString()),
        new Claim("UserId", payload.GetProperty("userId").GetString())
    };
    var identity = new ClaimsIdentity(claims, "CustomAuth");
    context.User = new ClaimsPrincipal(identity);

    await _next(context);
        }

        private bool ValidateToken(string token, out JsonElement payload)
        {
            payload = default;
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning($"Token has {parts.Length} parts (expected 3)");
                return false;
            }

            string header = parts[0];
            string payloadBase64Url = parts[1];
            string receivedSignature = parts[2];

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            byte[] headerPayloadBytes = Encoding.UTF8.GetBytes($"{header}.{payloadBase64Url}");
            byte[] computedSignatureBytes = hmac.ComputeHash(headerPayloadBytes);
            string computedSignature = Base64UrlHelper.Encode(computedSignatureBytes);

            // 1. Fixed: Use _logger instead of Console
            if (computedSignature != receivedSignature)
            {
                _logger.LogWarning($"Signature mismatch: {computedSignature} vs {receivedSignature}");
                return false;
            }

            try
            {
                byte[] payloadBytes = Base64UrlHelper.Decode(payloadBase64Url);
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);
                payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
            }
            catch
            {
                return false;
            }

            // 2. Fixed: Use payload instead of jsonElement
            long exp; // 3. Declare exp first
            if (!payload.TryGetProperty("exp", out var expElement) || 
                !expElement.TryGetInt64(out exp)) // Assign to exp
            {
                _logger.LogWarning("Token missing 'exp'");
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > exp)
            {
                _logger.LogWarning("Token expired");
                return false;
            }

            return true;
        }
    }



    public static class Base64UrlHelper
{
    public static string Encode(byte[] input)
    {
        // Convert to Base64 and replace URL-unsafe characters
        string base64 = Convert.ToBase64String(input);
        string base64Url = base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('='); // Remove padding

        return base64Url;
    }

    public static byte[] Decode(string input)
    {
        // Replace URL-safe characters and add padding
        string base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Pad the string to a multiple of 4
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}

    public static class CustomTokenMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomTokenAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CustomTokenMiddleware>();
        }
    }
}

