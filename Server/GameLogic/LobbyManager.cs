using System;
using System.Collections.Concurrent;
using System.Linq;
using Shared.Network.Messages;
using Server.Network;

namespace Server.GameLogic
{
    public class LobbyManager
    {
        private readonly int _maxPlayers;
        private readonly ConcurrentDictionary<int, LobbyPlayerInfo> _players;
        private readonly ServerNetworkManager _network;
        
        /// <summary>
        /// Fired when the lobby countdown completes and the match begins.
        /// Payload is the list of connected player IDs.
        /// </summary>
        public event Action<List<int>>? OnMatchStarted;

        public bool IsMatchStarted { get; private set; }
        private float _countdownTimer = -1f;

        public LobbyManager(ServerNetworkManager network, int maxPlayers)
        {
            _maxPlayers = maxPlayers;
            _players = new ConcurrentDictionary<int, LobbyPlayerInfo>();
            _network = network;
            
            _network.OnMessageReceived += HandleMessage;
            _network.OnClientDisconnected += HandleDisconnect;
        }

        public void Update(float delta)
        {
            if (IsMatchStarted || _countdownTimer < 0) return;

            _countdownTimer -= delta;

            if (_countdownTimer <= 0)
            {
                StartMatch();
            }
            else
            {
                // Optionally broadcast countdown updates every second if needed, 
                // but clients can also simulate it locally if we send the start time.
                // For now, we'll send periodic updates if preferred, or just rely on the first LobbyStateMessage Broadcast.
            }
        }

        private void StartMatch()
        {
            Console.WriteLine("[Lobby] Countdown terminato! Avvio del match...");
            IsMatchStarted = true;
            _countdownTimer = -1f;
            
            var startMsg = NetworkMessage.Create(MessageType.StartRound, new StartRoundMessage { RoundNumber = 1 });
            _network.BroadcastMessage(startMsg);
            
            OnMatchStarted?.Invoke(_players.Keys.ToList());
        }

        private void HandleMessage(int clientId, NetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.JoinLobby:
                    var joinMsg = message.DeserializePayload<JoinLobbyMessage>();
                    if (joinMsg != null)
                    {
                        OnPlayerJoined(clientId, joinMsg.PlayerName);
                    }
                    break;

                case MessageType.ReadyUp:
                    var readyMsg = message.DeserializePayload<ReadyUpMessage>();
                    if (readyMsg != null)
                    {
                        OnPlayerReadyStateChanged(clientId, readyMsg.IsReady);
                    }
                    break;
            }
        }

        private void HandleDisconnect(int clientId)
        {
            if (_players.TryRemove(clientId, out var playerInfo))
            {
                Console.WriteLine($"[Lobby] Giocatore {playerInfo.PlayerName} (ID: {clientId}) ha lasciato la lobby. ({_players.Count}/{_maxPlayers})");
                ResetCountdown();
                BroadcastLobbyState();
            }
        }

        private void OnPlayerJoined(int clientId, string originalName)
        {
            if (IsMatchStarted)
            {
                Console.WriteLine($"[Lobby] Rifiutato (ID: {clientId}): Match già in corso.");
                _network.DisconnectClient(clientId);
                return;
            }

            if (_players.Count >= _maxPlayers)
            {
                Console.WriteLine($"[Lobby] Rifiutato (ID: {clientId}): Lobby piena.");
                _network.DisconnectClient(clientId);
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(originalName) ? $"Player_{clientId}" : originalName;
            var playerInfo = new LobbyPlayerInfo
            {
                PlayerId = clientId,
                PlayerName = playerName,
                IsReady = false
            };

            if (_players.TryAdd(clientId, playerInfo))
            {
                Console.WriteLine($"[Lobby] Giocatore {playerName} (ID: {clientId}) si è unito. ({_players.Count}/{_maxPlayers})");
                
                var responseMsg = NetworkMessage.Create(MessageType.JoinLobbyResponse, new JoinLobbyMessage { PlayerName = playerName });
                _network.SendMessage(clientId, responseMsg);

                BroadcastLobbyState();
            }
        }

        private void OnPlayerReadyStateChanged(int clientId, bool isReady)
        {
            if (IsMatchStarted) return;

            if (_players.TryGetValue(clientId, out var playerInfo))
            {
                playerInfo.IsReady = isReady;
                Console.WriteLine($"[Lobby] Giocatore {playerInfo.PlayerName} (ID: {clientId}) è ora {(isReady ? "Ready" : "Not Ready")}.");
                
                CheckLobbyReady();
                BroadcastLobbyState();
            }
        }

        private void CheckLobbyReady()
        {
            if (_players.Count < _maxPlayers)
            {
                ResetCountdown();
                return;
            }

            bool allReady = _players.Values.All(p => p.IsReady);

            if (allReady && _countdownTimer < 0)
            {
                Console.WriteLine("[Lobby] Tutti i giocatori sono pronti. Avvio countdown (5s)...");
                _countdownTimer = 5.0f;
            }
            else if (!allReady && _countdownTimer >= 0)
            {
                Console.WriteLine("[Lobby] Giocatore non più pronto. Countdown interrotto.");
                ResetCountdown();
            }
        }

        private void ResetCountdown()
        {
            _countdownTimer = -1f;
        }

        public void BroadcastLobbyState()
        {
            var msg = new LobbyStateMessage
            {
                Players = _players.Values.ToList(),
                AllReady = _players.Count == _maxPlayers && _players.Values.All(p => p.IsReady),
                CountdownRemaining = _countdownTimer
            };

            var networkMsg = NetworkMessage.Create(MessageType.LobbyState, msg);
            _network.BroadcastMessage(networkMsg);
        }
    }
}
