// MainWindow_Database.cs

// Metoder og funktioner der bruges som er en del af pakker. 
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;

namespace ProjectR;

// Vinduet er en del af class: Mainwindow den er linket til vores mainwindow. 
// dette er det samme for vores andre cs filer. 
// Logik bag knap til recreatedatabase button, så hvad der sker når man klikker på knappen på Gui.
// Den sletter databasen, genskaber services, så den altså opretter tabeller for eksempelvis brugere og andet.
// og så giver den besked på at den har "Database recreated."., der sættes selfølgelig error på hvis det ikke fungere.
// Denne samme logik bruger gentagne gange til at anvende logikken bag de diverse knapper. 
public partial class MainWindow : Window
{
    public async void RecreateDatabaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _db.Database.EnsureDeletedAsync();
            InitServices();
            await SafeInitDbAsync();
            Log("Database recreated.");
        }
        catch (Exception ex)
        {
            Log("Recreate DB error: " + ex.Message);
        }
    }
    
    // ShowRunsbutton, her er det igen logikken bag knappen på gui. 
    // vi vil gerne have gemt batch runs i databasen og her bruger vi try funktionen til at tage de seneste batch runs og "list" them.
    // Her filtreres batch runs så, de er alt det der ikke er -1
    // 500 og 50, er 500 der eksempelvis gemmes og 50 der vises i log så der ikke kommer for mange. 
    // Log messages for latest batch runs ved brug af if statement.
    // Vigtig info: vi har brugt local time og utc nogle steder fordi, computeren det er lavet på, nogle gange ikke ville fungere ved brug af den ene eller den anden. 
    // dette er selfølgelig ikke ideelt, men vil kunne rettes senere hen. 
    public async void ShowRunsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var rows = await _db.SortingRuns
                .OrderByDescending(r => r.EndedAt)
                .Take(500)
                .ToListAsync();

            var batchRuns = rows
                .Where(r =>
                    r.ItemsCounted != -1 &&
                    TryParseBatchMeta(r.Username, out _, out _, out _, out _))
                .Take(50)
                .ToList();

            Log("---- Latest batch runs ----");
            if (batchRuns.Count == 0)
            {
                Log("(no batch runs saved yet)");
                Log("---------------------------");
                return;
            }

            foreach (var r in batchRuns)
            {
                var endedLocal = r.EndedAt.ToLocalTime();

                _ = TryParseBatchMeta(r.Username, out var who, out var batchCycles, out var secStartUtc, out var secEndUtc);

                Log($"{endedLocal:yy-MM-dd HH:mm:ss} | Batch finished");
                Log($"    User:         {who}");
                Log($"    Batch cycles:  {batchCycles}");
                Log($"    Security:      {secStartUtc:yyyy-MM-dd HH:mm:ss}Z  ->  {secEndUtc:yyyy-MM-dd HH:mm:ss}Z");
            }

            Log("---------------------------");
        }
        catch (Exception ex)
        {
            Log("Show runs error: " + ex.Message);
        }
    }

    // Parser batch meta format:
    // Hvad den gemmer, og viser os. 
    // "{user} | batchCycles=5 | securityStartUtc=... | securityEndUtc=..."
    // TryParseBatchMeta tager en tekststreng fra databasen og forsøger at udtrække brugernavn, antal batchcyklusser og start/sluttid for security.
    // Strengen splittes op ved hjælp af |, og hver del kontrolleres for kendte nøgleord.
    // Metoden bruger TryParse for at undgå crashes, hvis data er ugyldig.
    // Hvis alle værdier kan læses korrekt, returnerer metoden true.
    // Hvis noget mangler eller er forkert formateret, returnerer den false.
    private static bool TryParseBatchMeta(
        string? meta,
        out string username,
        out int batchCycles,
        out DateTime secStartUtc,
        out DateTime secEndUtc)
    {
        username = "unknown";
        batchCycles = 0;
        secStartUtc = default;
        secEndUtc = default;

        if (string.IsNullOrWhiteSpace(meta)) return false;

        var parts = meta.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;

        username = parts[0].Trim();

        bool okCycles = false, okStart = false, okEnd = false;

        foreach (var p in parts.Skip(1))
        {
            if (p.StartsWith("batchCycles=", StringComparison.OrdinalIgnoreCase))
            {
                okCycles = int.TryParse(p.Substring("batchCycles=".Length), out batchCycles);
            }
            else if (p.StartsWith("securityStartUtc=", StringComparison.OrdinalIgnoreCase))
            {
                okStart = DateTime.TryParse(
                    p.Substring("securityStartUtc=".Length),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out secStartUtc);
            }
            else if (p.StartsWith("securityEndUtc=", StringComparison.OrdinalIgnoreCase))
            {
                okEnd = DateTime.TryParse(
                    p.Substring("securityEndUtc=".Length),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out secEndUtc);
            }
        }

        return okCycles && okStart && okEnd;
    }

    // ShowAllLoginsButton_OnClick henter alle brugere fra databasen ved hjælp af Entity Framework.
    // brugerne sorteres alfabetisk efter brugernavn.
    // metoden viser enten en besked, hvis der ingen brugere er, eller lister alle brugere i loggen.
    // for hver bruger vises både brugernavn og om brugeren er administrator.
    // eventuelle fejl fanges, så programmet ikke crasher, men i stedet logger fejlen.
    public async void ShowAllLoginsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var users = await _db.Accounts
                .OrderBy(a => a.Username)
                .ToListAsync();

            Log("---- All logins ----");
            if (users.Count == 0)
            {
                Log("(no users)");
            }
            else
            {
                foreach (var u in users)
                    Log($"{u.Username,-18} | admin={(u.IsAdmin ? "YES" : "NO")}");
            }
            Log("--------------------");
        }
        catch (Exception ex)
        {
            Log("Show all logins error: " + ex.Message);
        }
    }

    // ShowEmergencyLogButton_OnClick hente alle nødstop hændelser fra databasen.
    // Kun rækker markeret som emergency filtreres og sorteres med den nyeste først.
    // For hver hændelse vises tidspunktet og hvilken bruger der aktiverede nødstop.
    // brugernavnet udtrækkes fa en tekststreng ved hjælp af TryParseEmergencyUser.
    // hvis der opstår fejl, fanges de og logges uden at programmet crasher.
    public async void ShowEmergencyLogButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var rows = await _db.SortingRuns
                .Where(r => r.ItemsCounted == -1 && (r.Username ?? "").StartsWith("EMERGENCY|"))
                .OrderByDescending(r => r.EndedAt)
                .Take(200)
                .ToListAsync();

            Log("---- Emergency log ----");
            if (rows.Count == 0)
            {
                Log("(no emergency clicks logged yet)");
                Log("-----------------------");
                return;
            }

            foreach (var r in rows)
            {
                var localTime = r.EndedAt.ToLocalTime();
                var who = TryParseEmergencyUser(r.Username) ?? "unknown";
                Log($"{localTime:yy-MM-dd HH:mm:ss} | user={who}");
            }

            Log("-----------------------");
        }
        catch (Exception ex)
        {
            Log("Show emergency log error: " + ex.Message);
        }
    }

    private static string? TryParseEmergencyUser(string? meta)
    {
        if (string.IsNullOrWhiteSpace(meta)) return null;

        var parts = meta.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.StartsWith("user=", StringComparison.OrdinalIgnoreCase))
                return p.Substring("user=".Length).Trim();
        }
        return null;
    }
}
