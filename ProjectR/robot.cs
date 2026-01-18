using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProjectR;

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

    public Robot(string ipAddress, int dashboardPort = 29999, int urscriptPort = 30003)
    {
        IpAddress = ipAddress;
        DashboardPort = dashboardPort;
        UrscriptPort = urscriptPort;
    }

    public bool Connected => _clientDashboard.Connected && _clientUrscript.Connected;

    public void Connect()
    {
        // Dashboard
        _clientDashboard.Connect(IpAddress, DashboardPort);
        _streamDashboard = _clientDashboard.GetStream();
        _readerDashboard = new StreamReader(_streamDashboard, Encoding.ASCII);
        _writerDashboard = new StreamWriter(_streamDashboard, Encoding.ASCII) { AutoFlush = true };

        // consume welcome line
        _ = _readerDashboard.ReadLine();

        // URScript (secondary interface)
        _clientUrscript.Connect(IpAddress, UrscriptPort);
        _streamUrscript = _clientUrscript.GetStream();
    }

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

    // ---------------- DASHBOARD ----------------

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

    public void SendDashboard(string command)
    {
        _ = SendDashboardAndReadLine(command);
    }

    /// <summary>
    /// Returns true if a program is currently running on the robot.
    /// </summary>
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

    // ---------------- URSCRIPT ----------------

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

    /// <summary>
    /// Secondary program: change IO without overwriting the main running program.
    /// </summary>
    public void SetStandardDigitalOut(int index, bool value)
    {
        var v = value ? "True" : "False";

        var program =
            "sec io_set():\n" +
            $"  set_standard_digital_out({index}, {v})\n" +
            "end\n";

        SendUrscript(program);
    }

    // ---------------- HARD STOP ----------------

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
---