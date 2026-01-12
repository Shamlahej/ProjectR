using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProjectR;

public class Robot
{
    private readonly string _ip;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public bool Connected => _client?.Connected == true;

    public Robot(string ipAddress)
    {
        _ip = ipAddress;
    }

    // Connecter kun til URScript-porten (30002)
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

        // URScript sendes som ASCII med newline til sidst
        var bytes = Encoding.ASCII.GetBytes(program + "\n");
        _stream.Write(bytes, 0, bytes.Length);
    }

    // Hjælper: læs script fra fil og send
    public void SendUrscriptFile(string filePath)
    {
        var script = File.ReadAllText(filePath);
        SendUrscript(script);
    }
}