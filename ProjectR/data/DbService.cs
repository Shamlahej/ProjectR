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

    public async Task<bool> EnsureCreatedAndSeedAsync()
    {
        var created = await _db.Database.EnsureCreatedAsync();

        // Sørg for counter-rækken findes
        var counter = await _db.Counters.FirstOrDefaultAsync(c => c.Id == 1);
        if (counter == null)
        {
            _db.Counters.Add(new Counter { Id = 1 });
            await _db.SaveChangesAsync();
        }

        return created;
    }

    public async Task IncrementSortedAsync(bool isOk)
    {
        var c = await _db.Counters.FirstAsync(x => x.Id == 1);
        c.ItemsSortedTotal += 1;
        if (isOk) c.ItemsOkTotal += 1;
        else c.ItemsRejectedTotal += 1;
        await _db.SaveChangesAsync();
    }

    public Task<Counter> GetCounterAsync()
        => _db.Counters.FirstAsync(x => x.Id == 1);
}