using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectR;

public class Robot
{
    private readonly string _ip;
    private TcpClient? _client;
    private NetworkStream? _stream;

    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;

    public bool Connected => _client?.Connected == true;

    public event Action<string>? RobotEventReceived;

    public Robot(string ipAddress)
    {
        _ip = ipAddress;
    }

    // Connecter til URScript-porten (30002)
    public async Task ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_ip, 30002);
        _stream = _client.GetStream();
    }

    // Sender URScript tekst til robotten
    public void SendUrscript(string program)
    {
        if (_stream == null)
            throw new InvalidOperationException("Ikke forbundet. Tryk Connect først.");

        var bytes = Encoding.ASCII.GetBytes(program + "\n");
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void SendUrscriptFile(string filePath)
    {
        var script = File.ReadAllText(filePath);
        SendUrscript(script);
    }

    // Lyt efter events fra robotten (robot.script socket_open -> din PC/Mac)
    public void StartEventListener(int port)
    {
        // allerede startet
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
