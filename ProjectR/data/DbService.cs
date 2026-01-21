// DbService or Dbcontext represents the interaction session with the database.

// Metoder og funktioner der bruges som er en del af pakker. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

// denne klasse bruges til at snakke med databasen på ét sted
// _db er selve forbindelsen til databasen
// det gør koden nemmere at forstå og ændre senere
// og mindsker risikoen for fejl i database-koden

public class DbService
{
    private readonly AppDbContext _db;

    public DbService(AppDbContext db)
    {
        _db = db;
    }

    // denne kode bruges til at sikre, at databasen og tabellerne findes
    // hvis databasen ikke findes endnu, bliver den oprettet automatisk
    
    public Task<bool> EnsureCreatedAsync()
        => _db.Database.EnsureCreatedAsync();

    // denne kode bruges til at gemme en batchrun i databasen
    // tidspunktet for hvornår kørslen slutter gemmes automatisk
    // resultatet af batchen eller en specialværdi gemmes (antal)
    // usernavnet gemmes, så man kan se hvem der stod bag kørslen
    // dataene gemmes, så systemets brug kan dokumenteres og spores

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
// denne kode bruges til at hente de nyeste kørsler fra databasen
// kørslerne sorteres efter tidspunkt, så de nyeste kommer først
// take bestemmer hvor mange kørsler der hentes
// resultatet bruges til at vise historik og log i programmet

    public Task<List<SortingRun>> GetLatestRunsAsync(int take)
        => _db.SortingRuns
            .OrderByDescending(r => r.EndedAt)
            .Take(take)
            .ToListAsync();
}
