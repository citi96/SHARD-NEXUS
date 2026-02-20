using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;

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

                // Inizia ad accettare i client in modo asincrono
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
            byte[] buffer = new byte[4096];

            try
            {
                while (_isRunning && client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        // Disconnessione
                        break;
                    }

                    // TODO: Processare i dati ricevuti (pacchetti minimi)
                    Console.WriteLine($"[Network] Ricevuti {bytesRead} bytes dal Client {clientId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Network] Errore o disconnessione forzata Client {clientId}: {ex.Message}");
            }
            finally
            {
                DisconnectClient(clientId);
            }
        }

        public void DisconnectClient(int clientId)
        {
            if (_clients.TryRemove(clientId, out TcpClient client))
            {
                client.Close();
                Console.WriteLine($"[Network] Client {clientId} disconnesso. ({_clients.Count}/{MaxClients})");
            }
        }
    }
}
