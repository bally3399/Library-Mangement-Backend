using fortunae.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace fortunae.Infrastructure.Data
{
    public class LibraryDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public LibraryDbContext(DbContextOptions<LibraryDbContext> options, IConfiguration configuration) 
            : base(options) 
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public LibraryDbContext() 
        {
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Borrowing> Borrowings { get; set; }
        public DbSet<Rating> Ratings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = _configuration["DATABASE_PUBLIC_URL"] 
                    ?? "Host=shinkansen.proxy.rlwy.net;Port=45474;Username=postgres;Password=gPUbFWPknMQftgIPCpcAmjzMzONxaXJf;Database=railway;SslMode=Require;TrustServerCertificate=true";
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Borrowing>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Borrowing>()
                .HasOne(b => b.Book)
                .WithMany()
                .HasForeignKey(b => b.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Borrowing>()
                .Property(b => b.Penalty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.Book)
                .WithMany(b => b.Ratings)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Book>()
                .Property(b => b.AverageRating)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Rating>()
                .Property(r => r.Value)
                .IsRequired();

            // Fix the CHECK constraint with quoted column name
            modelBuilder.Entity<Rating>()
                .HasCheckConstraint("CHK_Rating_Value", "\"Value\" BETWEEN 1 AND 5");

            modelBuilder.Entity<Rating>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("NOW()");
        }
    }
}