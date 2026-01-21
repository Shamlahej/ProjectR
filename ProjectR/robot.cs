// Robot.cs

// Metoder og funktioner der bruges som er en del af pakker.
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProjectR;

// denne klasse repræsenterer selve robotten i programmet
// ipadresse og porte gemmes, så vi ved hvor vi skal forbinde til robotten
// tcpclient bruges til at lave netværksforbindelse til robotten
// dashboardforbindelsen bruges til styring som start, stop og status
// urscriptforbindelsen bruges til at sende selve robotprogrammet
// streams bruges til at sende og modtage data over forbindelsen
// lock bruges for at sikre, at der ikke sendes flere dashboard kommandoer samtidig
// constructoren gemmer ip og porte
// standardporte er sat til urrobots faste porte, så de ikke skal skrives hver gang (vi har fået dem fra underviserens robot class)
// https://industrial-programming.aydos.de/week08/using-a-new-robot-class.html

// Dashboard er den del af gui, vi bruger til at styre og kontrollere robotten, fx starte og stoppe den, uden at sende selve robotprogrammet.
public sealed class Robot
{
    public string IpAddress { get; }
    public int DashboardPort { get; }
    public int UrscriptPort { get; }

    private readonly TcpClient _clientDashboard = new();
    private NetworkStream? _streamDashboard;
    private StreamReader? _readerDashboard;
    private StreamWriter? _writerDashboard;

    private readonly TcpClient _clientUrscript = new();
    private NetworkStream? _streamUrscript;

    private readonly object _dashboardLock = new();

    public Robot(string ipAddress, int dashboardPort = 29999, int urscriptPort = 30002)
    {
        IpAddress = ipAddress;
        DashboardPort = dashboardPort;
        UrscriptPort = urscriptPort;
    }

    // Connected fortæller om begge forbindelser til robotten er aktive
    // både dashboard forbindelsen og urscript forbindelsen skal være forbundet
    // Connect opretter selve forbindelsen til robotten
    // først forbindes der til dashboard porten, som bruges til styring og status
    // derefter oprettes streams, så der kan sendes og modtages tekst
    // robotten sender en velkomstbesked, som vi bare læser og ignorerer
    // til sidst forbindes der til urscript porten, som bruges til at sende robotprogram

    public bool Connected => _clientDashboard.Connected && _clientUrscript.Connected;

    public void Connect()
    {
        // Dashboard
        _clientDashboard.Connect(IpAddress, DashboardPort);
        _streamDashboard = _clientDashboard.GetStream();
        _readerDashboard = new StreamReader(_streamDashboard, Encoding.ASCII);
        _writerDashboard = new StreamWriter(_streamDashboard, Encoding.ASCII) { AutoFlush = true };

        // tekst read
        _ = _readerDashboard.ReadLine();

        // URScript kan sendes
        _clientUrscript.Connect(IpAddress, UrscriptPort);
        _streamUrscript = _clientUrscript.GetStream();
    }

    // ConnectAsync bliver public task, så resten af programmet kan bruge await, når der oprettes forbindelse
    // selvom Connect ikke er async, passer denne metode ind i resten af koden
    // det gør at gui’en ikke fryser, når der forbindes til robotten
    // Disconnect bruges, når forbindelsen til robotten ikke længere skal bruges
    // begge forbindelser lukkes, så der ikke står åbne netværksforbindelser
    // try og catch sørger for, at programmet lukker pænt, selv hvis noget allerede er lukket

    public Task ConnectAsync()
    {
        Connect();
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        try { _clientDashboard.Close(); } catch { }
        try { _clientUrscript.Close(); } catch { }
    }

    // Dashboard
    // denne kode bruges til at sende en kommando til robottens dashboard
    // først tjekkes det om dashboard forbindelsen er klar, ellers stoppes programmet
    // lock bruges, så der kun sendes én kommando ad gangen
    // kommandoen sendes til robotten som tekst
    // robotten svarer med en linje, som vi læser og returnerer

    private string SendDashboardAndReadLine(string command)
    {
        if (_writerDashboard == null || _readerDashboard == null)
            throw new InvalidOperationException("Dashboard ikke forbundet.");

        lock (_dashboardLock)
        {
            _writerDashboard.WriteLine(command);
            return _readerDashboard.ReadLine() ?? "";
        }
    }

    // SendDashboard bruges, når vi bare vil sende en kommando
    // svaret ignoreres, fordi vi ikke altid har brug for det
    public void SendDashboard(string command)
    {
        _ = SendDashboardAndReadLine(command);
    }
    
    // Is program running?
    // denne kode bruges til at finde ud af, om robotten kører et program lige nu
    // der sendes en forespørgsel til robotens dashboard med kommandoen "running"
    // robotten svarer med true eller false (bool), som tekst
    // hvis svaret indeholder "true", betyder det at programmet kører
    // hvis der sker en fejl, antages det at programmet ikke kører

    public bool IsProgramRunning()
    {
        try
        {
            // Typical response: "Program running: true" / "Program running: false"
            var resp = SendDashboardAndReadLine("running").Trim().ToLowerInvariant();
            return resp.Contains("true");
        }
        catch
        {
            return false;
        }
    }

    // URscript
    // denne kode bruges til at sende selve robotprogrammet til robotten
    // først tjekkes det om forbindelsen til urscript er klar
    // programmet sendes som almindelig tekst over netværket til robotten
    // der sikres at programmet slutter med en ny linje, så robotten kan læse det korrekt
    // SendUrscriptFile bruges, når robotprogrammet ligger i en fil
    // filens indhold læses ind og sendes videre til robotten på samme måde


    public void SendUrscript(string program)
    {
        if (_streamUrscript == null)
            throw new InvalidOperationException("URScript ikke forbundet.");

        if (!program.EndsWith("\n")) program += "\n";
        var bytes = Encoding.ASCII.GetBytes(program);
        _streamUrscript.Write(bytes, 0, bytes.Length);
    }

    public void SendUrscriptFile(string path)
    {
        var program = File.ReadAllText(path);
        SendUrscript(program);
    }

    // denne kode bruges til at tænde eller slukke den digitale output på robotten
    // index fortæller hvilken udgang vi vil styre, og value bestemmer om den skal være tændt eller slukket
    // true bliver oversat til "True" og false til "False", fordi robotten forventer tekst (bool)
    // der bygges et lille urscript program, som sætter den digitale udgang
    // programmet sendes til robotten, så signalet bliver sat fysisk

    public void SetStandardDigitalOut(int index, bool value)
    {
        var v = value ? "True" : "False";

        var program =
            "sec io_set():\n" +
            $"  set_standard_digital_out({index}, {v})\n" +
            "end\n";

        SendUrscript(program);
    }

    // Emergencystop (hårde stop)
    // denne kode bruges til at lave et emergencystop på robotten
    // først tjekkes det om dashboard forbindelsen er aktiv
    // hvis der er forbindelse, sendes kommandoen "stop" til robotten
    // kommandoen får robotten til at stoppe med det samme
    // fejl ignoreres, så nødstop ikke kan få programmet til at crashe


    public void EmergencyStop()
    {
        if (!_clientDashboard.Connected) return;
        try
        {
            _ = SendDashboardAndReadLine("stop");
        }
        catch { }
    }
}
