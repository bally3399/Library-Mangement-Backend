using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace fortunae.Infrastructure.Data
{
    public class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
    {
        public LibraryDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LibraryDbContext>();
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                ?? "Host=localhost;Port=5432;Username=postgres;Password=your_local_password;Database=railway";
            optionsBuilder.UseNpgsql(connectionString);
            return new LibraryDbContext(optionsBuilder.Options);
        }
    }
}