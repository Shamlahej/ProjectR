// Models.cs
// Vi har taget inspiration af teori og eksempel udarbejdet af underviseren ift. security.
// https://industrial-programming.aydos.de/security/protecting-credential-database-against-exploitation.html

// Metoder og funktioner der bruges som er en del af pakker. 
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ProjectR.data;

// AccountService
// denne klasse bruges til at håndtere users og login ift databasen
// den sørger for at kodeord aldrig gemmes direkte, men altid som hash + salt
// databasen (_db) bruges til at gemme og hente users
// passwordhasher (_hasher) bruges til at lave sikker hashing med salt
// det beskytter mod angreb som rainbow tables og gør systemet mere sikkert
// fra teori: prevents precomputed attacks like rainbow tables, so that the attackers must crack each password individually
// ensures that identical passwords have different hashes

public class AccountService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;

    public AccountService(AppDbContext db, PasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }
// denne kode kører, når der oprettes en ny user
// users kodeord bliver aldrig gemt direkte i databasen
// der genereres et tilfældigt "salt", som kombineres med kodeordet
// kodeord + salt hashes, så hver bruger får en unik hash
// til sidst gemmes user i databasen

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
// tjekker om username allerede findes i databasen
// bruges for at undgå at oprette to users med samme navn
// koden bruges ved login
// userens gemte salt og hash hentes fra databasen
// det indtastede kodeord kombineres med samme salt og hashes igen
// de to hashes sammenlignes for at afgøre om login er korrekt
// på den måde afsløres kodeordet aldrig, og øger sikkerheden

    public Task<bool> UsernameExistsAsync(string username)
        => _db.Accounts.AnyAsync(a => a.Username == username);

    public async Task<bool> CredentialsCorrectAsync(string username, string password)
    {
        var account = await _db.Accounts.FirstAsync(a => a.Username == username);
        return _hasher.PasswordCorrect(password, account.Salt, account.SaltedPasswordHash);
    }
// henter en user fra databasen
// bruges når vi har brug for users oplysninger efter login
    public Task<Account> GetAccountAsync(string username)
        => _db.Accounts.FirstAsync(a => a.Username == username);
}

// Password
// denne klasse har ansvar for at gøre kodeord sikre, før de gemmes eller tjekkes
// _saltLength bestemmer hvor langt det tilfældige salt skal være
// _hashIterations bestemmer hvor mange gange hash-funktionen køres
// værdierne er valgt for at beskytte mod angreb som rainbow tables og password cracking

public class PasswordHasher
{
    private readonly int _saltLength;
    private readonly int _hashIterations;

    public PasswordHasher(int saltLength = 128 / 8, int hashIterations = 600_000)
    {
        _saltLength = saltLength;
        _hashIterations = hashIterations;
    }
// koden gør at vi kan tjekke om et indtastet kodeord er korrekt
// kodeordet kombineres med det gemte salt og hashes igen med samme hash-funktion
// den nye hash sammenlignes med den hash der er gemt i databasen
// FixedTimeEquals bruges for at undgå timing-angreb, hvor man kan gætte sig frem
// pbkdf2 og sha256 gør bruteforce angreb langsommere og mere besværlige

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
// nu er det vi koder når et nyt kodeord skal gemmes første gang
// der genereres et tilfældigt salt, så hvert kodeord bliver unikt
// kodeord og salt kombineres og hashes sammen
// både salt og hash returneres, så de kan gemmes i databasen

    public (byte[] Salt, byte[] Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltLength);
        return (salt, Hash(salt, password));
    }
}

// Account
// denne klasse represents en user i databasen
// username er unik, så hver bruger kun findes én gang
// salt gemmer det tilfældige data, der bruges sammen med kodeordet
// saltedpasswordhash gemmer det hashede kodeord og ikke selve kodeordet
// isadmin viser om brugeren har administrator rettigheder, hvor de har adgang til både database og opret bruger samt robot.
// tilsammen beskytter det brugernes login-oplysninger mod misbrug og angreb

public class Account
{
    [Key] public string Username { get; set; } = "";

    public byte[] Salt { get; set; } = [];
    public byte[] SaltedPasswordHash { get; set; } = [];
    public bool IsAdmin { get; set; }
}

// SortingRun
// denne klasse er for en gemt kørsel i databasen
// hver kørsel får et unikt id, så den kan identificeres
// endedat gemmer tidspunktet for hvornår kørslen sluttede (i utc)
// itemscounted bruges til at gemme resultatet af batchen eller en specialværdi
// username bruges til at gemme hvem der startede batchen eller aktiverede emergency
// dataen bruges til dokumentation og samlet set til analyse af systemets drift

public class SortingRun
{
    [Key] public int Id { get; set; }

    public DateTime EndedAt { get; set; } = DateTime.UtcNow;

    public int ItemsCounted { get; set; }

    public string? Username { get; set; }
}
