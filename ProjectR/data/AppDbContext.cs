//AppDbContext.cs

// DbService or Dbcontext represents the interaction/connection session with the database.
// Vi har benyttet os af underviserens forslag til hvordan vi laver en database og hvordan vi indsætter tabeller. 
// https://industrial-programming.aydos.de/week09/creating-database-and-tables.html

// Metoder og funktioner der bruges som er en del af pakker. 
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

// denne klasse er forbindelsen mellem programmet og databasen
// den fortæller entity framework hvilke tabeller der findes i databasen
// accounts bruges til at gemme user og login-oplysninger
// sortingruns bruges til at gemme batchruns og emergencykørsler
// dbcontext sørger for at data kan gemmes og hentes på en nem måde

public class AppDbContext : DbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<SortingRun> SortingRuns => Set<SortingRun>();

    private readonly string _dbPath;
    
// vi koder til at der bestemmes hvor databasen skal ligge på computeren
// databasen gemmes i programmets egen mappe under local application data
// hvis mappen ikke findes, bliver den oprettet automatisk
// til sidst gemmes path til database filen
// det sikrer at databasen altid ligger et fast og sikkert sted
// Dette gør også at vi sikre at dataen er samlet et sted.
// sqlite er metode fra undervisning (downloaded)

    public AppDbContext()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectR"
        );
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "database.sqlite");
    }
// her fortæller vi entity framework hvilken type database der bruges
// sqlite vælges, fordi det er en simpel fil baseret database
// databasen kobles til den path, som blev oprettet tidligere
// det gør at programmet automatisk kan gemme og hente data

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }
}
