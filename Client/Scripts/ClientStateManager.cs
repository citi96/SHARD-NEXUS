#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Client.Scripts;

/// <summary>
/// Receives server messages, maintains a local snapshot of game state,
/// and fires events for UI/rendering systems to consume.
/// Contains no game logic — only state storage and event dispatch.
/// </summary>
public class ClientStateManager
{
    // ── Stored State ──────────────────────────────────────────────────────────

    public PlayerState?                                     OwnState            { get; private set; }
    public IReadOnlyDictionary<int, OtherPlayerInfoMessage> Opponents           => _opponents;
    public List<int>                                        ShopEchoIds         { get; private set; } = new();
    public int                                              CurrentRound        { get; private set; }
    public GamePhase                                        CurrentPhase        { get; private set; }
    public int?                                             CombatOpponentId    { get; private set; }
    public PlayerState?                                     CombatOpponentState { get; private set; }
    public int?                                             LastCombatWinnerId  { get; private set; }
    public int?                                             LastCombatDamage    { get; private set; }
    public int?                                             GameWinnerId        { get; private set; }
    public FeaturedMatchMessage?                            FeaturedMatch       { get; private set; }

    private readonly Dictionary<int, OtherPlayerInfoMessage> _opponents = new();

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<PlayerState>?               OnOwnStateChanged;
    public event Action<int, OtherPlayerInfoMessage>? OnOpponentInfoChanged;
    public event Action<List<int>>?                 OnShopChanged;
    public event Action<int, PlayerState>?          OnCombatStarted;
    public event Action<CombatSnapshotPayload>?     OnCombatSnapshot;
    public event Action<int, int>?                  OnCombatEnded;
    public event Action<int>?                       OnGameEnded;
    public event Action<int>?                       OnRoundStarted;
    public event Action<GamePhase, float>?          OnPhaseChanged;
    public event Action<int, int>?                  OnPlayerEliminated;
    public event Action<FeaturedMatchMessage>?      OnFeaturedMatchChanged;
    public event Action<ActionRejectedMessage>?     OnActionRejected;

    // ── Entry Point ───────────────────────────────────────────────────────────

    public void HandleMessage(NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.PlayerStateUpdate: HandlePlayerStateUpdate(message); break;
            case MessageType.OtherPlayerInfo:   HandleOtherPlayerInfo(message);   break;
            case MessageType.ShopRefreshed:     HandleShopRefreshed(message);     break;
            case MessageType.CombatStarted:     HandleCombatStarted(message);     break;
            case MessageType.CombatUpdate:      HandleCombatUpdate(message);      break;
            case MessageType.CombatEnded:       HandleCombatEnded(message);       break;
            case MessageType.GameEnded:         HandleGameEnded(message);         break;
            case MessageType.StartRound:        HandleStartRound(message);        break;
            case MessageType.PhaseChanged:      HandlePhaseChanged(message);      break;
            case MessageType.PlayerEliminated:  HandlePlayerEliminated(message);  break;
            case MessageType.FeaturedMatch:     HandleFeaturedMatch(message);     break;
            case MessageType.ActionRejected:    HandleActionRejected(message);    break;
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void HandlePlayerStateUpdate(NetworkMessage message)
    {
        var msg = message.DeserializePayload<PlayerStateUpdateMessage>();
        if (msg == null) return;
        OwnState = msg.State;
        OnOwnStateChanged?.Invoke(msg.State);
    }

    private void HandleOtherPlayerInfo(NetworkMessage message)
    {
        var msg = message.DeserializePayload<OtherPlayerInfoMessage>();
        if (msg == null) return;
        _opponents[msg.PlayerId] = msg;
        OnOpponentInfoChanged?.Invoke(msg.PlayerId, msg);
    }

    private void HandleShopRefreshed(NetworkMessage message)
    {
        var msg = message.DeserializePayload<ShopRefreshedMessage>();
        if (msg == null) return;
        ShopEchoIds = msg.EchoDefinitionIds;
        OnShopChanged?.Invoke(ShopEchoIds);
    }

    private void HandleCombatStarted(NetworkMessage message)
    {
        var msg = message.DeserializePayload<CombatStartedMessage>();
        if (msg == null) return;
        CombatOpponentId    = msg.OpponentId;
        CombatOpponentState = msg.OpponentState;
        OnCombatStarted?.Invoke(msg.OpponentId, msg.OpponentState);
    }

    private void HandleCombatUpdate(NetworkMessage message)
    {
        var msg = message.DeserializePayload<CombatUpdateMessage>();
        if (msg == null || string.IsNullOrEmpty(msg.EventJson)) return;
        var snapshot = JsonSerializer.Deserialize<CombatSnapshotPayload>(msg.EventJson);
        if (snapshot == null) return;
        OnCombatSnapshot?.Invoke(snapshot);
    }

    private void HandleCombatEnded(NetworkMessage message)
    {
        var msg = message.DeserializePayload<CombatEndedMessage>();
        if (msg == null) return;
        LastCombatWinnerId = msg.WinnerId;
        LastCombatDamage   = msg.DamageDealt;
        OnCombatEnded?.Invoke(msg.WinnerId, msg.DamageDealt);
    }

    private void HandleGameEnded(NetworkMessage message)
    {
        var msg = message.DeserializePayload<GameEndedMessage>();
        if (msg == null) return;
        GameWinnerId  = msg.WinnerId;
        CurrentPhase  = GamePhase.GameOver;
        OnGameEnded?.Invoke(msg.WinnerId);
    }

    private void HandleStartRound(NetworkMessage message)
    {
        var msg = message.DeserializePayload<StartRoundMessage>();
        if (msg == null) return;
        CurrentRound = msg.RoundNumber;
        OnRoundStarted?.Invoke(msg.RoundNumber);
    }

    private void HandlePhaseChanged(NetworkMessage message)
    {
        var msg = message.DeserializePayload<PhaseChangedMessage>();
        if (msg == null) return;
        CurrentPhase = msg.NewPhase;
        OnPhaseChanged?.Invoke(msg.NewPhase, msg.PhaseDurationSecs);
    }

    private void HandlePlayerEliminated(NetworkMessage message)
    {
        var msg = message.DeserializePayload<PlayerEliminatedMessage>();
        if (msg == null) return;
        OnPlayerEliminated?.Invoke(msg.PlayerId, msg.FinalPlacement);
    }

    private void HandleFeaturedMatch(NetworkMessage message)
    {
        var msg = message.DeserializePayload<FeaturedMatchMessage>();
        if (msg == null) return;
        FeaturedMatch = msg;
        OnFeaturedMatchChanged?.Invoke(msg);
    }

    private void HandleActionRejected(NetworkMessage message)
    {
        var msg = message.DeserializePayload<ActionRejectedMessage>();
        if (msg == null) return;
        OnActionRejected?.Invoke(msg);
    }
}
