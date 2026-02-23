using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Text;
using Shared.Network.Messages;

namespace Server.Network
{
    public class ServerNetworkManager
    {
        private const int Port = 7777;
        private const int MaxClients = 8;
        private TcpListener _listener;
        private ConcurrentDictionary<int, TcpClient> _clients;
        private int _clientCounter;
        private bool _isRunning;

        public event Action<int, NetworkMessage>? OnMessageReceived;
        public event Action<int>? OnClientDisconnected;

        public ServerNetworkManager()
        {
            _clients = new ConcurrentDictionary<int, TcpClient>();
            _clientCounter = 0;
            _isRunning = false;
        }

        public void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _isRunning = true;
                Console.WriteLine($"[Network] Server in ascolto sulla porta {Port}...");

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
            Console.WriteLine("[Network] Server arrestato e client disconnessi.");
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    
                    if (_clients.Count >= MaxClients)
                    {
                        Console.WriteLine("[Network] Connessione rifiutata: raggiunto il limite massimo di client.");
                        client.Close();
                        continue;
                    }

                    int clientId = ++_clientCounter;
                    if (_clients.TryAdd(clientId, client))
                    {
                        Console.WriteLine($"[Network] Client {clientId} connesso da {client.Client.RemoteEndPoint}. ({_clients.Count}/{MaxClients})");
                        // Qui si può avviare un task separato per gestire il singolo client (lettura/scrittura)
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
                    // Il listener è stato fermato, possiamo ignorare questo errore
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
                    // Leggi la lunghezza del messaggio (4 bytes)
                    int lengthBytesRead = await stream.ReadAsync(lengthBuffer, 0, 4);
                    if (lengthBytesRead == 0) break;

                    int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (jsonLength <= 0 || jsonLength > 1048576) // Max 1MB
                    {
                        Console.WriteLine($"[Network] Lunghezza messaggio non valida dal Client {clientId}");
                        break;
                    }

                    // Leggi i dati JSON
                    byte[] jsonBuffer = new byte[jsonLength];
                    int totalRead = 0;
                    while (totalRead < jsonLength)
                    {
                        int read = await stream.ReadAsync(jsonBuffer, totalRead, jsonLength - totalRead);
                        if (read == 0) throw new Exception("Connessione chiusa durante la lettura.");
                        totalRead += read;
                    }

                    string json = Encoding.UTF8.GetString(jsonBuffer);
                    var message = NetworkMessage.FromJson(json);
                    
                    if (message != null)
                    {
                        OnMessageReceived?.Invoke(clientId, message);
                    }
                }
            }
            catch (Exception) { /* disconnessione o errore lettura */ }
            finally
            {
                DisconnectClient(clientId);
            }
        }

        public void SendMessage(int clientId, NetworkMessage message)
        {
            if (_clients.TryGetValue(clientId, out TcpClient? client))
            {
                try
                {
                    string json = message.ToJson();
                    byte[] payload = Encoding.UTF8.GetBytes(json);
                    byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

                    NetworkStream stream = client.GetStream();
                    stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    stream.Write(payload, 0, payload.Length);
                }
                catch
                {
                    DisconnectClient(clientId);
                }
            }
        }

        public void BroadcastMessage(NetworkMessage message)
        {
            string json = message.ToJson();
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

            foreach (var kvp in _clients)
            {
                try
                {
                    NetworkStream stream = kvp.Value.GetStream();
                    stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    stream.Write(payload, 0, payload.Length);
                }
                catch
                {
                    DisconnectClient(kvp.Key);
                }
            }
        }

        public void DisconnectClient(int clientId)
        {
            if (_clients.TryRemove(clientId, out TcpClient? client))
            {
                client.Close();
                OnClientDisconnected?.Invoke(clientId);
                Console.WriteLine($"[Network] Connessione TCP chiusa per ID {clientId}. ({_clients.Count}/{MaxClients})");
            }
        }
    }
}
