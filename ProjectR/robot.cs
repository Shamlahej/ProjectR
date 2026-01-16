using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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

    private readonly TcpClient _clientUrscript = new();
    private NetworkStream? _streamUrscript;

    // Event listener (robot -> PC) for fx "COUNT"
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;

    public event Action<string>? RobotEventReceived;

    public Robot(string ipAddress, int dashboardPort = 29999, int urscriptPort = 30002)
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

        // consume welcome line
        _ = _readerDashboard.ReadLine();

        // URScript
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

    public void SendDashboard(string command)
    {
        if (_streamDashboard == null) throw new InvalidOperationException("Dashboard ikke forbundet.");
        if (!command.EndsWith("\n")) command += "\n";

        var bytes = Encoding.ASCII.GetBytes(command);
        _streamDashboard.Write(bytes, 0, bytes.Length);
    }

    public void SendUrscript(string program)
    {
        if (_streamUrscript == null) throw new InvalidOperationException("URScript ikke forbundet.");
        if (!program.EndsWith("\n")) program += "\n";

        var bytes = Encoding.ASCII.GetBytes(program);
        _streamUrscript.Write(bytes, 0, bytes.Length);
    }

    public void SendUrscriptFile(string path)
    {
        var program = File.ReadAllText(path);
        SendUrscript(program);
    }

    // --- DIGITAL OUTPUT (til DI-stop-løsning) ---
    public void SetStandardDigitalOut(int index, bool value)
    {
        // Sender en lille URScript-linje som sætter DO
        SendUrscript($"set_standard_digital_out({index}, {(value ? "True" : "False")})");
    }

    // --- HÅRDT STOP (Dashboard) ---
    public void EmergencyStop()
    {
        if (!_clientDashboard.Connected) return;
        SendDashboard("stop");
        _ = _readerDashboard?.ReadLine(); // consume response if any
    }

    // --- Event listener (COUNT etc.) ---
    public void StartEventListener(int port)
    {
        if (_listener != null) return;

        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleClient(client, token), token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                RobotEventReceived?.Invoke("LISTENER_ERROR: " + ex.Message);
            }
        }, token);
    }

    private void HandleClient(TcpClient client, CancellationToken token)
    {
        try
        {
            using (client)
            using (var ns = client.GetStream())
            using (var reader = new StreamReader(ns, Encoding.ASCII))
            {
                while (!token.IsCancellationRequested)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;

                    line = line.Trim();
                    if (line.Length == 0) continue;

                    RobotEventReceived?.Invoke(line);
                }
            }
        }
        catch (Exception ex)
        {
            RobotEventReceived?.Invoke("CLIENT_ERROR: " + ex.Message);
        }
    }

    public void StopEventListener()
    {
        try
        {
            _listenerCts?.Cancel();
            _listener?.Stop();
        }
        catch { }
        finally
        {
            _listener = null;
            _listenerCts = null;
        }
    }
}
---