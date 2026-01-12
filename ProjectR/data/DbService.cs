using System;
using System.IO;
using System.Linq;                 // <-- VIGTIG (Any/Single/FirstOrDefault)
using System.Security.Cryptography;
using System.Text;

namespace ProjectR.Data;

public static class DbService
{
    public static void Init()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();

        if (!db.Counters.Any(c => c.Id == 1))
        {
            db.Counters.Add(new Counter { Id = 1 });
            db.SaveChanges();
        }
    }

    public static void EnsureComponentType(string name, string? description = null)
    {
        using var db = new AppDbContext();
        if (!db.ComponentTypes.Any(x => x.Name == name))
        {
            db.ComponentTypes.Add(new ComponentType { Name = name, Description = description });
            db.SaveChanges();
        }
    }

    public static void LogSorting(int componentTypeId, bool isOk, int? userId = null)
    {
        using var db = new AppDbContext();

        db.SortingEvents.Add(new SortingEvent
        {
            ComponentTypeId = componentTypeId,
            IsOk = isOk,
            UserId = userId
        });

        var counter = db.Counters.Single(c => c.Id == 1);
        counter.ItemsSortedTotal += 1;
        if (isOk) counter.ItemsOkTotal += 1;
        else counter.ItemsRejectedTotal += 1;

        db.SaveChanges();
    }

    public static long GetItemsSortedTotal()
    {
        using var db = new AppDbContext();
        return db.Counters.Single(c => c.Id == 1).ItemsSortedTotal;
    }

    public static int RegisterUser(string username, string password)
    {
        using var db = new AppDbContext();

        if (db.Users.Any(u => u.Username == username))
            throw new Exception("Brugernavn findes allerede.");

        var user = new User
        {
            Username = username,
            PasswordHash = Hash(password)
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    public static int? Login(string username, string password)
    {
        using var db = new AppDbContext();
        var hash = Hash(password);
        var user = db.Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);
        return user?.Id;
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

        // kompatibel med alle .NET versioner:
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

