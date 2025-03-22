namespace fortunae.Infrastructure.Data
{
    using fortunae.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    // using Microsoft.EntityFrameworkCore;
    using Npgsql.EntityFrameworkCore.PostgreSQL;


    public class LibraryDbContext : DbContext
    {
        private readonly IConfiguration _cofiguration;
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) 
        { 
            _cofiguration = configuration;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Borrowing> Borrowings { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public IConfiguration? configuration { get; }

        public LibraryDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Provide a default connection string for migrations
                 var connectionString = _cofiguration.GetConnectionString("DefaultConnection");
                 optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Borrowing>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId);

            modelBuilder.Entity<Borrowing>()
                .HasOne(b => b.Book)
                .WithMany()
                .HasForeignKey(b => b.BookId);

            modelBuilder.Entity<Borrowing>()
                .Property(b => b.Penalty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.Book)
                .WithMany(b => b.Ratings)
                .HasForeignKey(r => r.BookId);

            modelBuilder.Entity<Book>()
                .Property(b => b.AverageRating)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId);

            modelBuilder.Entity<Rating>()
                .Property(r => r.Value)
                .IsRequired();

            modelBuilder.Entity<Rating>()
                .HasCheckConstraint("CHK_Rating_Value", "Value BETWEEN 1 AND 5");

            modelBuilder.Entity<Rating>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("GETDATE()");
        }

    }
}
