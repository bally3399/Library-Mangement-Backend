using fortunae.Infrastructure.Data;
using fortunae.Infrastructure.Interfaces;
using fortunae.Service.Interfaces;
using fortunae.Service.Services;
using fortunae.Infrastructure.Repositories;
using fortunae.Middleware;
using fortunae.Service.Services.CacheService;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using CloudinaryDotNet;
using AspNetCoreRateLimit;
using Amazon.S3;
using StackExchange.Redis;
using DotNetEnv;
using static fortunae.api.AppSettings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Amazon;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? throw new InvalidOperationException("REDIS_CONNECTION is not configured");
var multiplexer = ConnectionMultiplexer.Connect(redisConnection);

builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddScoped<IRedisService, RedisService>();


var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Program");

try
{
    logger.LogInformation("Starting web application");
}
catch (Exception ex)
{
    logger.LogError(ex, "Application start-up failed");
    throw;
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? throw new Exception("JWT_ISSUER is missing!");
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? throw new Exception("JWT_AUDIENCE is missing!");
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new Exception("JWT_SECRET_KEY is missing!");




var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? throw new InvalidOperationException("DB_CONNECTION is not configured");
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(connectionString));


builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    //options.InstanceName = "FortunaLibrary_";
});


builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            Console.WriteLine($"Received token: {context.Token}");
            return Task.CompletedTask;
        }
    };
});
var awsSettings = new AWSSettings
{
    AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
        ?? throw new InvalidOperationException("AWS_ACCESS_KEY_ID is not configured"),
    SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
        ?? throw new InvalidOperationException("AWS_SECRET_ACCESS_KEY is not configured"),
    S3BucketName = Environment.GetEnvironmentVariable("AWS_S3_BUCKET")
        ?? throw new InvalidOperationException("AWS_S3_BUCKET is not configured"),
    Region = Environment.GetEnvironmentVariable("AWS_REGION")
        ?? throw new InvalidOperationException("AWS_REGION is not configured")
};

builder.Services.AddSingleton(new AmazonS3Client(
    awsSettings.AccessKeyId,
    awsSettings.SecretAccessKey,
    RegionEndpoint.GetBySystemName(awsSettings.Region)
));

builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false);

var awsConfig = builder.Configuration.GetSection("AWS").Get<AWSSettings>();

builder.Services.Configure<AWSSettings>(options =>
{
    options.AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    options.SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    options.S3BucketName = Environment.GetEnvironmentVariable("AWS_S3_BUCKET");
    options.Region = Environment.GetEnvironmentVariable("AWS_REGION");
});

var cloudinarySettings = new CloudinarySettings
{
    CloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME")
        ?? throw new InvalidOperationException("CLOUDINARY_CLOUD_NAME is not configured"),
    ApiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")
        ?? throw new InvalidOperationException("CLOUDINARY_API_KEY is not configured"),
    ApiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET")
        ?? throw new InvalidOperationException("CLOUDINARY_API_SECRET is not configured")
};

builder.Services.AddSingleton(new Cloudinary(
    new Account(
        cloudinarySettings.CloudName,
        cloudinarySettings.ApiKey,
        cloudinarySettings.ApiSecret
    )
));


builder.Services.Configure<JwtSettings>(options =>
{
    options.Issuer = jwtIssuer;
    options.Audience = jwtAudience;
    options.SecretKey = jwtSecretKey;
    options.ExpirationTime = int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRATION") ?? "30");
});
builder.Services.Configure<JwtSettings>(options =>
    builder.Configuration.GetSection("JwtSettings").Bind(options));
builder.Services.Configure<AWSSettings>(options =>
    builder.Configuration.GetSection("AWS").Bind(options));
builder.Services.Configure<CloudinarySettings>(options =>
    builder.Configuration.GetSection("Cloudinary").Bind(options));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IBorrowingService, BorrowingService>();
builder.Services.AddScoped<IBorrowingRepository, BorrowingRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

builder.Services.AddAWSService<IAmazonS3>();
var cloudinaryAccount = new Account(
    Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"),
    Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"),
    Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET")
);
builder.Services.AddSingleton(new Cloudinary(cloudinaryAccount));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<LibraryDbContext>();
        dbContext.Database.Migrate();
        Console.WriteLine("? Database migration applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Error applying migrations: {ex.Message}");
    }
}
app.UseForwardedHeaders();


if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(policy => policy
    .SetIsOriginAllowed(_ => true)
    .AllowAnyMethod()
    .AllowAnyHeader()
);

app.UseFortunaExceptionHandler();
app.UseIpRateLimiting();
builder.Services.AddHealthChecks();

app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
    }
});




app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();