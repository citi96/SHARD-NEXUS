using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Text;
using Shared.Network.Messages;

namespace Server.Network;

public class ServerNetworkManager
{
    private readonly int _port;
    private readonly int _maxClients;
    private readonly int _ackTimeoutMs;
    private readonly int _ackMaxRetries;
    private const int MaxMessageSize = 1_048_576; // 1 MB

    private TcpListener? _listener;
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
    private int _clientCounter;
    private bool _isRunning;

    // --- ACK tracking ---
    private readonly ConcurrentDictionary<uint, PendingAck> _pendingAcks = new();
    private uint _broadcastSeqCounter = uint.MaxValue / 2; // Start high to avoid collisions with NetworkMessage counter

    // --- Events ---
    public event Action<int, NetworkMessage>? OnMessageReceived;
    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;

    // --- Lifecycle ---

    public ServerNetworkManager(int maxClients, int port, int ackTimeoutMs, int ackMaxRetries)
    {
        _maxClients = maxClients;
        _port = port;
        _ackTimeoutMs = ackTimeoutMs;
        _ackMaxRetries = ackMaxRetries;
    }

    public void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[Network] Server in ascolto sulla porta {_port}...");
            Task.Run(AcceptClientsAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Errore durante l'avvio del server: {ex.Message}");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();

        foreach (var client in _clients.Values)
        {
            client.Close();
        }
        _clients.Clear();
        _pendingAcks.Clear();
        Console.WriteLine("[Network] Server arrestato e client disconnessi.");
    }

    /// <summary>
    /// Call periodically (e.g. every tick) to check for ACK timeouts and retry.
    /// </summary>
    public void Update()
    {
        long now = Environment.TickCount64;

        foreach (var kvp in _pendingAcks)
        {
            var pending = kvp.Value;
            if (now - pending.SentAtMs > _ackTimeoutMs)
            {
                if (pending.Retries >= _ackMaxRetries)
                {
                    Console.WriteLine($"[Network] ACK timeout per seq {kvp.Key} verso client {pending.ClientId} dopo {_ackMaxRetries} tentativi.");
                    _pendingAcks.TryRemove(kvp.Key, out _);
                    // Optionally disconnect the client
                }
                else
                {
                    pending.Retries++;
                    pending.SentAtMs = now;
                    Console.WriteLine($"[Network] Retry {pending.Retries}/{_ackMaxRetries} per seq {kvp.Key} verso client {pending.ClientId}");
                    SendRaw(pending.ClientId, pending.Message);
                }
            }
        }
    }

    // --- Connection handling ---

    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                TcpClient client = await _listener!.AcceptTcpClientAsync();

                if (_clients.Count >= _maxClients)
                {
                    Console.WriteLine("[Network] Connessione rifiutata: raggiunto il limite massimo di client.");
                    client.Close();
                    continue;
                }

                int clientId = Interlocked.Increment(ref _clientCounter);
                if (_clients.TryAdd(clientId, client))
                {
                    Console.WriteLine($"[Network] Client {clientId} connesso da {client.Client.RemoteEndPoint}. ({_clients.Count}/{_maxClients})");
                    OnClientConnected?.Invoke(clientId);
                    _ = Task.Run(() => HandleClientAsync(clientId, client));
                }
                else
                {
                    Console.WriteLine("[Network] Errore interno durante l'aggiunta del client.");
                    client.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Network] Errore nell'accettare un client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(int clientId, TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] lengthBuffer = new byte[4];

        try
        {
            while (_isRunning && client.Connected)
            {
                int lengthBytesRead = await ReadExactAsync(stream, lengthBuffer, 4);
                if (lengthBytesRead == 0) break;

                int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (jsonLength <= 0 || jsonLength > MaxMessageSize)
                {
                    Console.WriteLine($"[Network] Lunghezza messaggio non valida dal Client {clientId}: {jsonLength}");
                    break;
                }

                byte[] jsonBuffer = new byte[jsonLength];
                int totalRead = await ReadExactAsync(stream, jsonBuffer, jsonLength);
                if (totalRead == 0) break;

                string json = Encoding.UTF8.GetString(jsonBuffer);
                var message = NetworkMessage.FromJson(json);

                if (message != null)
                {
                    HandleInternalMessage(clientId, message);
                }
            }
        }
        catch (Exception) { /* disconnessione o errore lettura */ }
        finally
        {
            DisconnectClient(clientId);
        }
    }

    /// <summary>
    /// Read exactly 'count' bytes from the stream. Returns 0 if connection closed.
    /// </summary>
    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
            if (read == 0) return 0;
            totalRead += read;
        }
        return totalRead;
    }

    // --- Internal message handling (Ping, Ack) ---

    private void HandleInternalMessage(int clientId, NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.Ping:
                HandlePing(clientId, message);
                return;

            case MessageType.Ack:
                HandleAck(message);
                return;
        }

        // If message requires ACK, send one back
        if (message.RequiresAck)
        {
            var ack = NetworkMessage.Create(MessageType.Ack, new AckMessage { AcknowledgedSequenceId = message.SequenceId });
            SendRaw(clientId, ack);
        }

        // Forward to game logic
        OnMessageReceived?.Invoke(clientId, message);
    }

    private void HandlePing(int clientId, NetworkMessage message)
    {
        var ping = message.DeserializePayload<PingMessage>();
        if (ping == null) return;

        var pong = NetworkMessage.Create(MessageType.Pong, new PongMessage
        {
            OriginalTimestamp = ping.Timestamp,
            ServerReceivedAt = DateTime.UtcNow.Ticks
        });
        SendRaw(clientId, pong);
    }

    private void HandleAck(NetworkMessage message)
    {
        var ack = message.DeserializePayload<AckMessage>();
        if (ack != null)
        {
            _pendingAcks.TryRemove(ack.AcknowledgedSequenceId, out _);
        }
    }

    // --- Sending ---

    public void SendMessage(int clientId, NetworkMessage message)
    {
        if (message.RequiresAck)
        {
            _pendingAcks.TryAdd(message.SequenceId, new PendingAck
            {
                ClientId = clientId,
                Message = message,
                SentAtMs = Environment.TickCount64,
                Retries = 0
            });
        }
        SendRaw(clientId, message);
    }

    public void BroadcastMessage(NetworkMessage message)
    {
        foreach (var kvp in _clients)
        {
            if (message.RequiresAck)
            {
                // Each broadcast needs unique tracking per-client, clone with fresh SequenceId
                var clone = new NetworkMessage
                {
                    Type = message.Type,
                    PayloadJson = message.PayloadJson,
                    SequenceId = Interlocked.Increment(ref _broadcastSeqCounter),
                    RequiresAck = true
                };
                _pendingAcks.TryAdd(clone.SequenceId, new PendingAck
                {
                    ClientId = kvp.Key,
                    Message = clone,
                    SentAtMs = Environment.TickCount64,
                    Retries = 0
                });
                SendRaw(kvp.Key, clone);
            }
            else
            {
                SendRaw(kvp.Key, message);
            }
        }
    }

    private void SendRaw(int clientId, NetworkMessage message)
    {
        if (_clients.TryGetValue(clientId, out TcpClient? client))
        {
            try
            {
                string json = message.ToJson();
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

                NetworkStream stream = client.GetStream();
                lock (stream) // Prevent interleaved writes from concurrent sends
                {
                    stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    stream.Write(payload, 0, payload.Length);
                }
            }
            catch
            {
                DisconnectClient(clientId);
            }
        }
    }

    // --- Disconnect ---

    public void DisconnectClient(int clientId)
    {
        if (_clients.TryRemove(clientId, out TcpClient? client))
        {
            client.Close();
            OnClientDisconnected?.Invoke(clientId);
            Console.WriteLine($"[Network] Connessione TCP chiusa per ID {clientId}. ({_clients.Count}/{_maxClients})");
        }
    }

    public int ConnectedClientCount => _clients.Count;

    // --- ACK tracking helper ---

    private class PendingAck
    {
        public int ClientId { get; set; }
        public NetworkMessage Message { get; set; } = null!;
        public long SentAtMs { get; set; }
        public int Retries { get; set; }
    }
}
