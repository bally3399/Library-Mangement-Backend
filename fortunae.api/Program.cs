// fortunae.api/Program.cs
using fortunae.Infrastructure.Data;
using fortunae.Infrastructure.Interfaces;
using fortunae.Service.Interfaces;
using fortunae.Service.Services;
using fortunae.Infrastructure.Repositories;
using fortunae.Middleware;
using fortunae.Service.Services.CacheService;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using AspNetCoreRateLimit;
using StackExchange.Redis;
using DotNetEnv;
using static fortunae.api.AppSettings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using fortunae.Service.Authentication;
using fortunae.Service.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;


Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// Database
var connectionString = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
    ?? throw new InvalidOperationException("Database connection string is missing.");
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? throw new InvalidOperationException("REDIS_CONNECTION is not configured");
var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IBorrowingService, BorrowingService>();
builder.Services.AddScoped<IBorrowingRepository, BorrowingRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IRedisService, RedisService>();

// Rate Limiting
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

// Authentication and Authorization
builder.Services.AddAuthentication("CustomScheme")
    .AddScheme<CustomTokenAuthenticationOptions, CustomTokenAuthenticationHandler>("CustomScheme", options =>
    {
        options.SecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
            ?? builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(options.SecretKey))
            throw new ArgumentException("JWT Secret Key is missing.");
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// Controllers and Swagger
builder.Services.AddControllers();
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

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    try
    {
        dbContext.Database.Migrate();
        Console.WriteLine("Database migration applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations: {ex.Message}");
    }
}

// Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled for Docker HTTP testing
app.UseRouting();
app.UseCors(policy => policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader());
app.UseFortunaExceptionHandler();
app.UseIpRateLimiting();
app.UseAuthentication(); // CustomTokenAuthenticationHandler
app.UseAuthorization();
app.UseCustomTokenAuthentication(); // CustomTokenMiddleware

app.MapControllers();
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

app.Run();