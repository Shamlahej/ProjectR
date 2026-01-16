using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

public class AppDbContext : DbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<SortingRun> SortingRuns => Set<SortingRun>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectR"
        );
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "database.sqlite");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }
}
---