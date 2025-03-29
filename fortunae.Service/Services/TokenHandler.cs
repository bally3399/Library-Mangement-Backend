

// fortunae.Service/Authentication/CustomTokenAuthenticationHandler.cs


using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace fortunae.Service.Authentication
{
    public class CustomTokenAuthenticationOptions : AuthenticationSchemeOptions
    {
       public string SecretKey {get; set;} = "a571f2aa6e0a7d7fd793ec6344afe6b0d9b75bcc62886bb388a810cf8e94dc58d0ba960c68975ed9d45cd3877d6d69becfe25371b31f51470b1a5ee9ff3cfbb640b19c63f699ba0e97c854622260934de8346add1fd53a6fb34a3072ba1c60a520c261f5404ad928194847c6278c8de22161a1a84f58fde55bf442d1954b49a7397979fc6ed04921e222424fa56c244e239dc8d63ff5abee6d6bb648d8d78c2155ffd67fef5afa556eb40dc486ac871efcc3bacb430c8dfb61e0fb01fb826ec653a9692ab2c8f4c7c58a66221c8018dbf3a83e0010ca1eeff36c7b5aaea3988069cf1db8475ce5368236768f3a0b54730c879fc374f10530f668a7a5567fef71";
    }

    public class CustomTokenAuthenticationHandler : AuthenticationHandler<CustomTokenAuthenticationOptions>
    {
        private readonly ILogger<CustomTokenAuthenticationHandler> _logger;
        private readonly string _secretKey; // Store secret key here

        public CustomTokenAuthenticationHandler(
    IOptionsMonitor<CustomTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : base(
        options ?? throw new ArgumentNullException(nameof(options)),
        logger ?? throw new ArgumentNullException(nameof(logger)),
        encoder ?? throw new ArgumentNullException(nameof(encoder))
    )
        {
             // Add explicit null checks (even though base call validates options, logger, encoder)
    _ = configuration ?? throw new ArgumentNullException(nameof(configuration));

          _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
        ?? configuration["Jwt:Key"] 
        ?? options.CurrentValue.SecretKey;

          if (string.IsNullOrEmpty(_secretKey))
            {
                _logger.LogError("Secret key is missing.");
                throw new ArgumentException("Secret key is missing.");
            }

    _logger = logger.CreateLogger<CustomTokenAuthenticationHandler>();
             _logger.LogInformation($"Using secret key: {_secretKey}");
            // In CustomTokenAuthenticationHandler.ValidateToken():
_logger.LogInformation($"SECRET KEY (VALIDATION): {_secretKey}");
            _logger.LogDebug("Initializing CustomTokenAuthenticationHandler");
            if (configuration == null)
            {
                _logger.LogWarning("IConfiguration is null, relying on options or environment.");
            }

             // Resolve the secret key
        // _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
        //     ?? configuration?["Jwt:Key"]
        //     ?? Options.SecretKey; // Fallback to default in options
        //      _logger.LogInformation($"Using secret key: {Options.SecretKey}");

        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var path = Context.Request.Path.Value?.ToLower();
            if (path != null &&
                (path.StartsWith("/api/auth/register") || path.StartsWith("/api/auth/login")))
            {
                return AuthenticateResult.NoResult();
            }

            if (!Context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                !authHeader.ToString().StartsWith("Bearer "))
            {
                return AuthenticateResult.Fail("Authorization header missing or invalid.");
            }

            var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
            _logger.LogInformation($"Token received: {token}, Parts: {token.Split('.').Length}");
            if (!ValidateToken(token, out var payload))
            {
                return AuthenticateResult.Fail("Invalid or expired token.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, payload.GetProperty("sub").GetString()),
                new Claim(ClaimTypes.Role, payload.GetProperty("role").GetString()),
                new Claim("UserId", payload.GetProperty("userId").GetString())
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        private bool ValidateToken(string token, out JsonElement payload)
        {
            payload = default;
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning($"Token does not have exactly 3 parts. Found: {parts.Length}");
                return false;
            }

            string headerBase64Url = parts[0];
            string payloadBase64Url = parts[1];
            string receivedSignature = parts[2];

            // Use _secretKey instead of Options.SecretKey
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            byte[] headerPayloadBytes = Encoding.UTF8.GetBytes($"{headerBase64Url}.{payloadBase64Url}");
                byte[] computedSignatureBytes = hmac.ComputeHash(headerPayloadBytes);
                 string computedSignature = Base64UrlHelper.Encode(computedSignatureBytes); // 👈 Use helper

                  // Debug logs
    _logger.LogInformation($"Computed Signature: {computedSignature}");
    _logger.LogInformation($"Received Signature: {receivedSignature}");


             // 2. Compare signatures
    if (computedSignature != receivedSignature)
    {
        _logger.LogWarning("Signature verification failed");
        return false;
    }

    // 3. Decode payload with Base64Url
    try
    {
        byte[] payloadBytes = Base64UrlHelper.Decode(payloadBase64Url); // 👈 Use helper
        string payloadJson = Encoding.UTF8.GetString(payloadBytes);
        payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Payload deserialization failed: {ex.Message}");
        return false;
    }

    // 4. Validate expiration
    if (!payload.TryGetProperty("exp", out var expElement) || 
        !expElement.TryGetInt64(out long exp) || 
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
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