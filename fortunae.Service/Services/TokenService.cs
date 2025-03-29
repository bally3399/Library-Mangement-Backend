using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
//using System.IdentityModel.Tokens.Jwt;

public class CustomTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public CustomTokenService(string secretKey, string issuer, string audience, int expirationMinutes = 30)
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
        _expirationMinutes = expirationMinutes;
    }

    public string GenerateToken(string username, string role, string userId)
    {
        // Validate inputs
        if (string.IsNullOrEmpty(_secretKey) || _secretKey.Length < 32)
            throw new ArgumentException("Secret key must be at least 32 characters long");

        // Create header
        var header = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            alg = "HS256",
            typ = "JWT"
        }));

        // Create payload
        var payload = Base64UrlEncode(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "sub", username },
            { "jti", Guid.NewGuid().ToString() },
            { "role", role },
            { "userId", userId },
            { "iss", _issuer },
            { "aud", _audience },
            { "exp", DateTimeOffset.UtcNow.AddMinutes(_expirationMinutes).ToUnixTimeSeconds() },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        }));

        // Create signature
        var signature = CreateSignature(header, payload);

        // Combine all parts
        return $"{header}.{payload}.{signature}";
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            // Split the token into parts
            var parts = token.Split('.');
            if (parts.Length != 3)
                throw new ArgumentException("Invalid token format");

            var header = parts[0];
            var payload = parts[1];
            var signature = parts[2];

            // Verify signature
            var computedSignature = CreateSignature(header, payload);
            if (computedSignature != signature)
                throw new SecurityTokenInvalidSignatureException("Invalid token signature");

            // Decode payload
            var payloadJson = Base64UrlDecode(payload);
            var payloadDict = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

            // Validate standard claims
            ValidateClaims(payloadDict);

            // Create claims principal
            var claims = new List<Claim>();
            foreach (var claim in payloadDict)
            {
                claims.Add(new Claim(claim.Key, claim.Value?.ToString() ?? ""));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "custom"));
        }
        catch (Exception ex)
        {
            throw new SecurityTokenException("Token validation failed", ex);
        }
    }

    private void ValidateClaims(Dictionary<string, object> payload)
    {
        // Validate issuer
        if (!payload.TryGetValue("iss", out var issuer) || issuer?.ToString() != _issuer)
            throw new SecurityTokenInvalidIssuerException("Invalid token issuer");

        // Validate audience
        if (!payload.TryGetValue("aud", out var audience) || audience?.ToString() != _audience)
            throw new SecurityTokenInvalidAudienceException("Invalid token audience");

        // Validate expiration
        if (!payload.TryGetValue("exp", out var expiration) || 
            long.TryParse(expiration?.ToString(), out long exp) == false || 
            DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow)
            throw new SecurityTokenExpiredException("Token has expired");
    }

    private string CreateSignature(string header, string payload)
    {
        // Create the signing input
        var signingInput = $"{header}.{payload}";

        // Create HMAC-SHA256 signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return Base64UrlEncode(Convert.ToBase64String(hash));
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string Base64UrlDecode(string input)
    {
        // Add padding if needed
        input = input.PadRight(input.Length + (4 - input.Length % 4) % 4, '=')
            .Replace("-", "+")
            .Replace("_", "/");

        var bytes = Convert.FromBase64String(input);
        return Encoding.UTF8.GetString(bytes);
    }

    // Custom exception classes
    public class SecurityTokenException : Exception
    {
        public SecurityTokenException(string message) : base(message) { }
        public SecurityTokenException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class SecurityTokenInvalidSignatureException : SecurityTokenException
    {
        public SecurityTokenInvalidSignatureException(string message) : base(message) { }
    }

    public class SecurityTokenInvalidIssuerException : SecurityTokenException
    {
        public SecurityTokenInvalidIssuerException(string message) : base(message) { }
    }

    public class SecurityTokenInvalidAudienceException : SecurityTokenException
    {
        public SecurityTokenInvalidAudienceException(string message) : base(message) { }
    }

    public class SecurityTokenExpiredException : SecurityTokenException
    {
        public SecurityTokenExpiredException(string message) : base(message) { }
    }
}