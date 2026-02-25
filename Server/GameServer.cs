using Server.Network;
using Server.GameLogic;
using Server.Configuration;
using Shared.Data;
using Shared.Models.Structs;
using Shared.Models.Enums;
using Shared.Network.Messages;
using System.Collections.Generic;
using System.Linq;

namespace Server;

public class GameServer
{
    private readonly MatchManager _matchManager;
    private readonly ServerNetworkManager _networkManager;
    private readonly LobbyManager _lobbyManager;
    private readonly EchoPoolManager _echoPoolManager;
    private readonly PlayerManager _playerManager;
    private readonly ShopManager _shopManager;
    private readonly CombatManager _combatManager;
    private readonly MatchmakingManager _matchmakingManager;
    private readonly List<EchoDefinition> _echoCatalog;
    private readonly Random _rng;

    private int _currentRound;
    private bool _isRunning;

    public GameServer(
        int maxPlayers,
        int port,
        int ackTimeoutMs,
        int ackMaxRetries,
        EchoPoolSettings echoPoolSettings,
        ShopSettings shopSettings,
        PlayerSettings playerSettings,
        CombatSettings combatSettings)
    {
        _matchManager = new MatchManager();
        _networkManager = new ServerNetworkManager(maxPlayers, port, ackTimeoutMs, ackMaxRetries);
        _lobbyManager = new LobbyManager(_networkManager, maxPlayers);
        _playerManager = new PlayerManager(playerSettings);
        _matchmakingManager = new MatchmakingManager();
        _rng = new Random(Environment.TickCount);
        _echoCatalog = new List<EchoDefinition>(EchoCatalog.All);

        _echoPoolManager = new EchoPoolManager(echoPoolSettings, _echoCatalog);
        _shopManager = new ShopManager(shopSettings, _echoPoolManager, _playerManager, _echoCatalog);
        _combatManager = new CombatManager(_playerManager, _networkManager, _echoCatalog, combatSettings);

        WireEvents();
    }

    private void WireEvents()
    {
        _shopManager.OnShopUpdated += (id, msg) => _networkManager.SendMessage(id, msg);
        _playerManager.OnPlayerStateChanged += OnPlayerStateChanged;
        _lobbyManager.OnMatchStarted += OnMatchStarted;
        _combatManager.OnAllCombatsComplete += OnAllCombatsComplete;
        _networkManager.OnMessageReceived += HandleMessage;
        _networkManager.OnClientConnected += HandleClientConnected;
        _networkManager.OnClientDisconnected += _playerManager.RemovePlayer;
    }

    private void OnPlayerStateChanged(int playerId, PlayerState state)
    {
        var full = NetworkMessage.Create(MessageType.PlayerStateUpdate,
            new PlayerStateUpdateMessage { State = state });
        _networkManager.SendMessage(playerId, full);

        var partial = NetworkMessage.Create(MessageType.OtherPlayerInfo,
            new OtherPlayerInfoMessage
            {
                PlayerId = playerId,
                NexusHealth = state.NexusHealth,
                Level = state.Level,
                WinStreak = state.WinStreak,
                LossStreak = state.LossStreak,
            });
        _networkManager.BroadcastMessage(partial);
    }

    private void OnMatchStarted(List<int> playerIds)
    {
        _currentRound = 1;
        StartNextCombatRound(playerIds);
    }

    private void OnAllCombatsComplete(List<CombatResult> results)
    {
        UpdateMatchmakingHistory(results);
        GrantEconomyToAllPlayers();

        var aliveIds = GetAlivePlayerIds();

        if (aliveIds.Count >= 2)
        {
            _currentRound++;
            StartNextCombatRound(aliveIds);
        }
        else
        {
            int winnerId = aliveIds.Count == 1 ? aliveIds[0] : -1;
            BroadcastGameEnded(winnerId);
        }
    }

    private void GrantEconomyToAllPlayers()
    {
        foreach (int id in _playerManager.GetAllPlayerIds())
        {
            _playerManager.GrantEndOfRoundGold(id);
            _playerManager.GrantAutoXp(id);
        }
    }

    private void UpdateMatchmakingHistory(List<CombatResult> results)
    {
        foreach (var result in results)
        {
            if (result.WinnerPlayerId == MatchmakingManager.GhostPlayerId ||
                result.LoserPlayerId == MatchmakingManager.GhostPlayerId)
                continue;

            var winnerState = _playerManager.GetPlayerState(result.WinnerPlayerId);
            if (winnerState.HasValue)
                _matchmakingManager.RecordRoundResult(
                    result.WinnerPlayerId,
                    result.LoserPlayerId,
                    winnerState.Value);
        }
    }

    private List<int> GetAlivePlayerIds() =>
        _playerManager.GetAllPlayerIds()
            .Where(id => (_playerManager.GetPlayerState(id)?.NexusHealth ?? 0) > 0)
            .ToList();

    private void StartNextCombatRound(List<int> playerIds)
    {
        var states = playerIds
            .Select(id => _playerManager.GetPlayerState(id))
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (states.Count == 0) return;

        var mmResult = _matchmakingManager.ComputePairings(states, _currentRound, _rng);

        if (mmResult.Featured != null)
            BroadcastFeaturedMatch(mmResult.Featured);

        int seed = Environment.TickCount ^ _currentRound;
        _combatManager.StartCombatRound(mmResult.Pairs, _currentRound, seed);
    }

    private void BroadcastFeaturedMatch(FeaturedMatchInfo featured)
    {
        var msg = NetworkMessage.Create(MessageType.FeaturedMatch,
            new FeaturedMatchMessage
            {
                Player1Id = featured.Player1Id,
                Player2Id = featured.Player2Id,
                Reason = featured.Reason,
            });
        _networkManager.BroadcastMessage(msg);
    }

    private void BroadcastGameEnded(int winnerId)
    {
        var msg = NetworkMessage.Create(MessageType.GameEnded,
            new GameEndedMessage { WinnerId = winnerId, Placements = Array.Empty<int>() });
        _networkManager.BroadcastMessage(msg);
    }

    private void HandleClientConnected(int clientId)
    {
        _playerManager.InitializePlayer(clientId);
        _shopManager.GenerateShop(clientId);
    }

    private void HandleMessage(int clientId, NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.RefreshShop:
                _shopManager.HandleRefresh(clientId);
                break;

            case MessageType.BuyEcho:
                var buyMsg = message.DeserializePayload<BuyEchoMessage>();
                if (buyMsg != null)
                    _shopManager.HandleBuy(clientId, buyMsg.ShopSlot);
                break;

            case MessageType.BuyXP:
                _playerManager.HandleBuyXP(clientId);
                break;

            case MessageType.SellEcho:
                var sellMsg = message.DeserializePayload<SellEchoMessage>();
                if (sellMsg != null)
                    _shopManager.HandleSell(clientId, sellMsg.EchoInstanceId);
                break;

            case MessageType.PositionEcho:
                var posMsg = message.DeserializePayload<PositionEchoMessage>();
                if (posMsg == null) break;
                if (posMsg.BoardX < 0 || posMsg.BoardX > 3 ||
                    posMsg.BoardY < 0 || posMsg.BoardY > 3)
                {
                    Console.WriteLine($"[Server] PositionEcho rifiutato: coordinate non valide ({posMsg.BoardX},{posMsg.BoardY}) da client {clientId}");
                    break;
                }
                int boardIndex = posMsg.BoardY * 4 + posMsg.BoardX;
                bool moved = _playerManager.TryMoveToBoard(clientId, posMsg.EchoInstanceId, boardIndex);
                if (!moved)
                    Console.WriteLine($"[Server] PositionEcho rifiutato: TryMoveToBoard fallito (instanceId={posMsg.EchoInstanceId}, idx={boardIndex}, client={clientId})");
                break;
        }
    }

    public void Start()
    {
        _networkManager.Start();
        _isRunning = true;
    }

    public async Task RunAsync()
    {
        Start();
        long lastTick = Environment.TickCount64;

        while (_isRunning)
        {
            long now = Environment.TickCount64;
            float delta = (now - lastTick) / 1000f;
            lastTick = now;

            _networkManager.Update();
            _lobbyManager.Update(delta);
            _combatManager.Update(delta);

            await Task.Delay(16);
        }

        _networkManager.Stop();
    }

    public void Stop() => _isRunning = false;
}
