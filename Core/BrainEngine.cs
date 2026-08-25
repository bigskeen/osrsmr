using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsrsMr.Core
{
    public class BrainEngine
    {
        private static BrainEngine? _instance;
        public static BrainEngine Instance => _instance ??= new BrainEngine();

        public GameState State { get; } = new();

        public event Action<string, string>? OnRawPacketReceived;
        public event Action? OnStateUpdated;
        public event Action<bool>? OnConnectionStatusChanged;
        public event Action<string>? OnLogMessage;

        private TcpListener? _server;
        private CancellationTokenSource? _cts;
        private bool _isRunning;
        private bool _isConnected;

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnConnectionStatusChanged?.Invoke(value);
                }
            }
        }

        public void Start(int port = 43594)
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => ListenLoop(port, _cts.Token));
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            try { _server?.Stop(); } catch { }
            IsConnected = false;
        }

        private async Task ListenLoop(int port, CancellationToken ct)
        {
            try
            {
                _server = new TcpListener(IPAddress.Loopback, port);
                _server.Start();
                Log($"[BrainEngine] Server listening on 127.0.0.1:{port}");

                while (!ct.IsCancellationRequested)
                {
                    TcpClient client = await _server.AcceptTcpClientAsync(ct);
                    Log($"[BrainEngine] Client connected from {client.Client.RemoteEndPoint}");
                    IsConnected = true;

                    _ = Task.Run(() => HandleClientAsync(client, ct), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"[BrainEngine] Listener error: {ex.Message}");
            }
            finally
            {
                IsConnected = false;
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                try
                {
                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        string? line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        ProcessLine(line);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[BrainEngine] Stream disconnected: {ex.Message}");
                }
                finally
                {
                    IsConnected = false;
                    Log("[BrainEngine] Client disconnected");
                }
            }
        }

        public void ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            int colonIdx = line.IndexOf(':');
            if (colonIdx == -1) return;

            string key = line.Substring(0, colonIdx).Trim();
            string value = line.Substring(colonIdx + 1).Trim();

            OnRawPacketReceived?.Invoke(key, value);
            PacketDecoder.Decode(State, key, value);
            OnStateUpdated?.Invoke();
        }

        private void Log(string msg)
        {
            OnLogMessage?.Invoke(msg);
        }
    }
}
