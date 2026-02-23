using System;
using System.Collections.Concurrent;
using System.Linq;
using Shared.Network.Messages;
using Server.Network;

namespace Server.GameLogic
{
    public class LobbyManager
    {
        private const int MaxPlayers = 8;
        private ConcurrentDictionary<int, string> _connectedPlayers;
        private ServerNetworkManager _network;
        
        public bool IsMatchStarted { get; private set; }

        public LobbyManager(ServerNetworkManager network)
        {
            _connectedPlayers = new ConcurrentDictionary<int, string>();
            _network = network;
            _network.OnMessageReceived += HandleMessage;
            _network.OnClientDisconnected += HandleDisconnect;
        }

        private void HandleMessage(int clientId, NetworkMessage message)
        {
            if (message.Type == MessageType.JoinLobby)
            {
                var joinMsg = message.DeserializePayload<JoinLobbyMessage>();
                if (joinMsg != null)
                {
                    OnPlayerJoined(clientId, joinMsg.PlayerName);
                }
            }
        }

        private void HandleDisconnect(int clientId)
        {
            if (_connectedPlayers.TryRemove(clientId, out string? playerName))
            {
                Console.WriteLine($"[Lobby] Giocatore {playerName} (ID: {clientId}) ha lasciato la lobby. ({_connectedPlayers.Count}/{MaxPlayers})");
            }
        }

        private void OnPlayerJoined(int clientId, string playerName)
        {
            if (IsMatchStarted)
            {
                Console.WriteLine($"[Lobby] Rifiutato {playerName} (ID: {clientId}): Match già in corso.");
                _network.DisconnectClient(clientId);
                return;
            }

            if (_connectedPlayers.Count >= MaxPlayers)
            {
                Console.WriteLine($"[Lobby] Rifiutato {playerName} (ID: {clientId}): Lobby piena.");
                _network.DisconnectClient(clientId);
                return;
            }

            // Usa un nome di default se vuoto
            string finalName = string.IsNullOrWhiteSpace(playerName) ? $"Player_{clientId}" : playerName;
            
            if (_connectedPlayers.TryAdd(clientId, finalName))
            {
                Console.WriteLine($"[Lobby] Giocatore {finalName} (ID: {clientId}) si è unito. ({_connectedPlayers.Count}/{MaxPlayers})");
                
                // Opzionale: notifica il giocatore dell'avvenuto ingresso
                var responseMsg = NetworkMessage.Create(MessageType.JoinLobbyResponse, new JoinLobbyMessage { PlayerName = finalName });
                _network.SendMessage(clientId, responseMsg);

                CheckLobbyFull();
            }
        }

        private void CheckLobbyFull()
        {
            if (_connectedPlayers.Count == MaxPlayers && !IsMatchStarted)
            {
                Console.WriteLine("[Lobby] Lobby piena! Avvio del match...");
                IsMatchStarted = true;
                
                var startMsg = NetworkMessage.Create(MessageType.StartRound, new StartRoundMessage { RoundNumber = 1 });
                _network.BroadcastMessage(startMsg);
                
                // TODO: Notifica il MatchManager
            }
        }
    }
}
