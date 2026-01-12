using Microsoft.EntityFrameworkCore;

namespace ProjectR.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ComponentType> ComponentTypes => Set<ComponentType>();
    public DbSet<SortingEvent> SortingEvents => Set<SortingEvent>();
    public DbSet<Counter> Counters => Set<Counter>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Lægger databasen i din projektmappe (så du kan se den i Rider)
        optionsBuilder.UseSqlite("Data Source=../../../../app.sqlite");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed: vi sikrer at der findes en Counter række med Id=1
        modelBuilder.Entity<Counter>().HasData(new Counter
        {
            Id = 1,
            ItemsSortedTotal = 0,
            ItemsOkTotal = 0,
            ItemsRejectedTotal = 0
        });

        base.OnModelCreating(modelBuilder);
    }
}