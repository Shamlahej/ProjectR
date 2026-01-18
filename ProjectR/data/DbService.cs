using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

public class DbService
{
    private readonly AppDbContext _db;

    public DbService(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> EnsureCreatedAsync()
        => _db.Database.EnsureCreatedAsync();

    public async Task SaveRunAsync(int itemsCounted, string? username)
    {
        _db.SortingRuns.Add(new SortingRun
        {
            EndedAt = DateTime.UtcNow,
            ItemsCounted = itemsCounted,
            Username = username
        });

        await _db.SaveChangesAsync();
    }

    public Task<List<SortingRun>> GetLatestRunsAsync(int take)
        => _db.SortingRuns
            .OrderByDescending(r => r.EndedAt)
            .Take(take)
            .ToListAsync();
}
---