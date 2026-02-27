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
    private readonly PhaseManager _phaseManager;
    private readonly List<EchoDefinition> _echoCatalog;
    private readonly Random _rng;

    private const int PlayerBoardCols = 4;
    private const int PlayerBoardRows = 4;

    private bool _isRunning;

    public GameServer(
        int maxPlayers,
        int port,
        int ackTimeoutMs,
        int ackMaxRetries,
        EchoPoolSettings echoPoolSettings,
        ShopSettings shopSettings,
        PlayerSettings playerSettings,
        CombatSettings combatSettings,
        InterventionSettings interventionSettings,
        ResonanceSettings resonanceSettings,
        PhaseSettings phaseSettings)
    {
        _matchManager = new MatchManager();
        _networkManager = new ServerNetworkManager(maxPlayers, port, ackTimeoutMs, ackMaxRetries);
        _lobbyManager = new LobbyManager(_networkManager, maxPlayers);
        _playerManager = new PlayerManager(playerSettings, resonanceSettings);
        _phaseManager = new PhaseManager(phaseSettings);
        _matchmakingManager = new MatchmakingManager();
        _rng = new Random(Environment.TickCount);
        _echoCatalog = new List<EchoDefinition>(EchoCatalog.All);

        _echoPoolManager = new EchoPoolManager(echoPoolSettings, _echoCatalog);
        _shopManager = new ShopManager(shopSettings, _echoPoolManager, _playerManager, _echoCatalog);
        _combatManager = new CombatManager(_playerManager, _networkManager, _echoCatalog, combatSettings, interventionSettings, resonanceSettings);

        WireEvents();
    }

    private void WireEvents()
    {
        _shopManager.OnShopUpdated += (id, msg) => _networkManager.SendMessage(id, msg);
        _playerManager.OnPlayerStateChanged += OnPlayerStateChanged;
        _lobbyManager.OnMatchStarted += OnLobbyMatchStarted;
        _phaseManager.OnPhaseChanged += OnPhaseChanged;
        _combatManager.OnAllCombatsComplete += OnAllCombatsComplete;
        _combatManager.OnActionRejected += SendActionRejected;
        _playerManager.OnEchoFused += OnEchoFused;
        _networkManager.OnMessageReceived += HandleMessage;
        _networkManager.OnClientConnected += HandleClientConnected;
        _networkManager.OnClientDisconnected += _playerManager.RemovePlayer;
    }

    private void OnEchoFused(int playerId, FusionProcessor.FusionResult fusion)
    {
        var msg = NetworkMessage.Create(MessageType.EchoFused, new EchoFusedMessage
        {
            ResultInstanceId = fusion.ResultInstanceId,
            NewStarLevel = fusion.NewStarLevel,
            DefinitionId = fusion.DefinitionId,
            IsOnBoard = fusion.IsOnBoard,
            SlotIndex = fusion.SlotIndex,
        });
        _networkManager.SendMessage(playerId, msg);
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

    private void OnLobbyMatchStarted(List<int> playerIds)
    {
        _phaseManager.StartGame();
    }

    private void OnPhaseChanged(GamePhase newPhase, float duration)
    {
        Console.WriteLine($"[GameServer] Phase Changed: {newPhase} ({duration}s)");

        // Broadcast phase change to all clients
        var msg = NetworkMessage.Create(MessageType.PhaseChanged, new PhaseChangedMessage
        {
            NewPhase = newPhase,
            PhaseDurationSecs = duration
        });
        _networkManager.BroadcastMessage(msg);

        switch (newPhase)
        {
            case GamePhase.Preparation:
                HandlePreparationStart();
                break;
            case GamePhase.Combat:
                HandleCombatStart();
                break;
        }
    }

    private void HandlePreparationStart()
    {
        // For Preparation, ensure shops are refreshed for the new round
        foreach (int id in _playerManager.GetAllPlayerIds())
        {
            _shopManager.GenerateShop(id);
        }
    }

    private void HandleCombatStart()
    {
        var aliveIds = GetAlivePlayerIds();
        if (aliveIds.Count < 2)
        {
            int winnerId = aliveIds.Count == 1 ? aliveIds[0] : -1;
            _phaseManager.EndGame();
            BroadcastGameEnded(winnerId);
            return;
        }

        StartNextCombatRound(aliveIds);
    }

    private void OnAllCombatsComplete(List<CombatResult> results)
    {
        UpdateMatchmakingHistory(results);
        GrantEconomyToAllPlayers();

        // Move to Reward phase automatically
        _phaseManager.TriggerRewardPhase();
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

        var mmResult = _matchmakingManager.ComputePairings(states, _phaseManager.CurrentRound, _rng);

        if (mmResult.Featured != null)
            BroadcastFeaturedMatch(mmResult.Featured);

        int seed = Environment.TickCount ^ _phaseManager.CurrentRound;
        _combatManager.StartCombatRound(mmResult.Pairs, _phaseManager.CurrentRound, seed);
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

        // Sync current phase to newly joined client
        var msg = NetworkMessage.Create(MessageType.PhaseChanged, new PhaseChangedMessage
        {
            NewPhase = _phaseManager.CurrentPhase,
            PhaseDurationSecs = _phaseManager.TimeRemaining
        });
        _networkManager.SendMessage(clientId, msg);
    }

    private void SendActionRejected(int clientId, string action, string reason)
    {
        var msg = NetworkMessage.Create(MessageType.ActionRejected,
            new ActionRejectedMessage { Action = action, Reason = reason });
        _networkManager.SendMessage(clientId, msg);
    }

    private void HandleMessage(int clientId, NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.RefreshShop:
                if (!RequirePhase(clientId, "RefreshShop", GamePhase.Preparation)) break;
                if (!_shopManager.HandleRefresh(clientId))
                    SendActionRejected(clientId, "RefreshShop", "Oro insufficiente");
                break;

            case MessageType.BuyEcho:
                if (!RequirePhase(clientId, "BuyEcho", GamePhase.Preparation)) break;
                var buyMsg = message.DeserializePayload<BuyEchoMessage>();
                if (buyMsg != null && !_shopManager.HandleBuy(clientId, buyMsg.ShopSlot))
                    SendActionRejected(clientId, "BuyEcho", "Oro insufficiente o bench piena");
                break;

            case MessageType.BuyXP:
                if (!RequirePhase(clientId, "BuyXP", GamePhase.Preparation)) break;
                _playerManager.HandleBuyXP(clientId);
                break;

            case MessageType.SellEcho:
                var sellMsg = message.DeserializePayload<SellEchoMessage>();
                if (sellMsg != null)
                    _shopManager.HandleSell(clientId, sellMsg.EchoInstanceId);
                break;

            case MessageType.PositionEcho:
                if (!RequirePhase(clientId, "PositionEcho", GamePhase.Preparation)) break;
                var posMsg = message.DeserializePayload<PositionEchoMessage>();
                if (posMsg == null) break;
                if (posMsg.BoardX < 0 || posMsg.BoardX >= PlayerBoardCols ||
                    posMsg.BoardY < 0 || posMsg.BoardY >= PlayerBoardRows)
                {
                    SendActionRejected(clientId, "PositionEcho", "Coordinate non valide");
                    break;
                }
                int boardIndex = posMsg.BoardY * PlayerBoardCols + posMsg.BoardX;
                if (!_playerManager.TryMoveToBoard(clientId, posMsg.EchoInstanceId, boardIndex))
                    SendActionRejected(clientId, "PositionEcho", "Unità non in bench, slot occupato o limite livello raggiunto");
                break;

            case MessageType.RemoveFromBoard:
                if (!RequirePhase(clientId, "RemoveFromBoard", GamePhase.Preparation)) break;
                var removeMsg = message.DeserializePayload<RemoveFromBoardMessage>();
                if (removeMsg == null) break;
                if (!_playerManager.TryMoveToBench(clientId, removeMsg.EchoInstanceId))
                    SendActionRejected(clientId, "RemoveFromBoard", "Unità non in board o bench piena");
                break;

            case MessageType.UseIntervention:
                if (!RequirePhase(clientId, "UseIntervention", GamePhase.Combat)) break;
                var intMsg = message.DeserializePayload<UseInterventionMessage>();
                if (intMsg == null) break;
                if (!Enum.TryParse<InterventionType>(intMsg.CardId, out var intType))
                {
                    SendActionRejected(clientId, "UseIntervention", "Tipo intervento non valido");
                    break;
                }
                _combatManager.TryQueueIntervention(clientId, intType, intMsg.TargetId);
                break;
        }
    }

    /// <summary>
    /// Returns true if <see cref="PhaseManager.CurrentPhase"/> matches <paramref name="required"/>.
    /// Otherwise sends an ActionRejected message and returns false, so callers can break immediately.
    /// </summary>
    private bool RequirePhase(int clientId, string action, GamePhase required)
    {
        if (_phaseManager.CurrentPhase == required) return true;
        SendActionRejected(clientId, action, $"Azione non disponibile nella fase corrente (richiede: {required})");
        return false;
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
            _phaseManager.Update(delta);
            _combatManager.Update(delta);

            await Task.Delay(16);
        }

        _networkManager.Stop();
    }

    public void Stop() => _isRunning = false;
}
