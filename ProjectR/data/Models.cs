using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

// -------------------- SERVICES --------------------

public class AccountService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;

    public AccountService(AppDbContext db, PasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task NewAccountAsync(string username, string password, bool isAdmin = false)
    {
        var (salt, saltedPasswordHash) = _hasher.Hash(password);

        _db.Add(new Account
        {
            Username = username,
            Salt = salt,
            SaltedPasswordHash = saltedPasswordHash,
            IsAdmin = isAdmin
        });

        await _db.SaveChangesAsync();
    }

    public Task<bool> UsernameExistsAsync(string username)
        => _db.Accounts.AnyAsync(a => a.Username == username);

    public async Task<bool> CredentialsCorrectAsync(string username, string password)
    {
        var account = await _db.Accounts.FirstAsync(a => a.Username == username);
        return _hasher.PasswordCorrect(password, account.Salt, account.SaltedPasswordHash);
    }

    public Task<Account> GetAccountAsync(string username)
        => _db.Accounts.FirstAsync(a => a.Username == username);
}

public class PasswordHasher
{
    private readonly int _saltLength;
    private readonly int _hashIterations;

    public PasswordHasher(int saltLength = 128 / 8, int hashIterations = 600_000)
    {
        _saltLength = saltLength;
        _hashIterations = hashIterations;
    }

    public bool PasswordCorrect(string password, byte[] salt, byte[] saltedPasswordHash)
    {
        var hash = Hash(salt, password);
        return CryptographicOperations.FixedTimeEquals(hash, saltedPasswordHash);
    }

    private byte[] Hash(byte[] salt, string password)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _hashIterations,
            HashAlgorithmName.SHA256,
            256 / 8
        );
    }

    public (byte[] Salt, byte[] Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltLength);
        return (salt, Hash(salt, password));
    }
}

// -------------------- ENTITIES --------------------

public class Account
{
    [Key] public string Username { get; set; } = "";

    public byte[] Salt { get; set; } = [];
    public byte[] SaltedPasswordHash { get; set; } = [];
    public bool IsAdmin { get; set; }
}

public class Counter
{
    // Vi har kun én række: Id=1
    [Key] public int Id { get; set; } = 1;

    public long ItemsSortedTotal { get; set; }
    public long ItemsOkTotal { get; set; }
    public long ItemsRejectedTotal { get; set; }
}

// Gem “robotten sorterede X” for hver gang man stopper
public class SortingRun
{
    [Key] public int Id { get; set; }

    public DateTime EndedAt { get; set; } = DateTime.UtcNow;

    public int ItemsCounted { get; set; }

    // Optional: hvem kørte den (kan stå tom)
    public string? Username { get; set; }
}
---