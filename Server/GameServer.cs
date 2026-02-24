using Server.Network;
using Server.GameLogic;
using Server.Configuration;
using Shared.Models.Structs;
using Shared.Models.Enums;
using Shared.Network.Messages;
using System.Collections.Generic;

namespace Server;

public class GameServer
{
    private MatchManager _matchManager;
    private ServerNetworkManager _networkManager;
    private LobbyManager _lobbyManager;
    private EchoPoolManager _echoPoolManager;
    private PlayerManager _playerManager;
    private ShopManager _shopManager;
    private CombatManager _combatManager;
    private readonly List<EchoDefinition> _echoCatalog;
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

        // Mock Catalog for Echo Pool initialization
        _echoCatalog = new List<EchoDefinition>
        {
            new EchoDefinition(1, "Pyroth", Rarity.Common,    EchoClass.Vanguard,  Resonance.Fire,      500, 100,  50, 20, new int[]{}),
            new EchoDefinition(2, "Aquos",  Rarity.Uncommon,  EchoClass.Caster,    Resonance.Frost,     300, 200,  70, 15, new int[]{}),
            new EchoDefinition(3, "Terron", Rarity.Rare,      EchoClass.Vanguard,  Resonance.Earth,     800,  50,  30, 60, new int[]{}),
            new EchoDefinition(4, "Zephyr", Rarity.Epic,      EchoClass.Assassin,  Resonance.Lightning, 400, 150,  90, 10, new int[]{}),
            new EchoDefinition(5, "Lumin",  Rarity.Legendary, EchoClass.Support,   Resonance.Light,     600, 300,  40, 40, new int[]{})
        };

        _echoPoolManager = new EchoPoolManager(echoPoolSettings, _echoCatalog);
        _shopManager = new ShopManager(shopSettings, _echoPoolManager, _playerManager, _echoCatalog);
        _combatManager = new CombatManager(_playerManager, _networkManager, _echoCatalog, combatSettings);

        // Wiring up events
        _shopManager.OnShopUpdated += (playerId, msg) => _networkManager.SendMessage(playerId, msg);

        _playerManager.OnPlayerStateChanged += (playerId, state) =>
        {
            // Send full state to the owner
            var updateMsg = NetworkMessage.Create(Shared.Network.Messages.MessageType.PlayerStateUpdate, new Shared.Network.Messages.PlayerStateUpdateMessage { State = state });
            _networkManager.SendMessage(playerId, updateMsg);

            // Send partial state to everyone else
            var otherInfo = NetworkMessage.Create(MessageType.OtherPlayerInfo, new OtherPlayerInfoMessage
            {
                PlayerId = playerId,
                NexusHealth = state.NexusHealth,
                Level = state.Level,
                WinStreak = state.WinStreak,
                LossStreak = state.LossStreak
            });

            // Broadcast to all connected clients. Clients should ignore updates where PlayerId matches their own.
            _networkManager.BroadcastMessage(otherInfo);
        };

        // When lobby countdown completes, start combat for the paired players
        _lobbyManager.OnMatchStarted += playerIds =>
        {
            if (playerIds.Count >= 2)
            {
                int seed = Environment.TickCount;
                _combatManager.StartCombat(playerIds[0], playerIds[1], round: 1, seed: seed);
            }
        };

        // Listen to client messages
        _networkManager.OnMessageReceived += HandleMessage;
        _networkManager.OnClientConnected += HandleClientConnected;
        _networkManager.OnClientDisconnected += _playerManager.RemovePlayer;
    }

    private void HandleClientConnected(int clientId)
    {
        // For testing purposes, initialize player state immediately upon connection.
        // In reality, this would happen when a match starts.
        _playerManager.InitializePlayer(clientId);

        // Give them an initial shop just so we have something on screen!
        _shopManager.GenerateShop(clientId);
    }

    private void HandleMessage(int clientId, Shared.Network.Messages.NetworkMessage message)
    {
        switch (message.Type)
        {
            case Shared.Network.Messages.MessageType.RefreshShop:
                _shopManager.HandleRefresh(clientId);
                break;
            case Shared.Network.Messages.MessageType.BuyEcho:
                var buyMsg = message.DeserializePayload<Shared.Network.Messages.BuyEchoMessage>();
                if (buyMsg != null)
                {
                    _shopManager.HandleBuy(clientId, buyMsg.ShopSlot);
                }
                break;
        }
    }

    public void Start()
    {
        Console.WriteLine("Server avviato. Inizializzazione moduli in corso...");
        _networkManager.Start();
        _isRunning = true;
    }

    /// <summary>
    /// Main server loop. Call this to run the server tick (~60Hz).
    /// Blocks until Stop() is called.
    /// </summary>
    public async Task RunAsync()
    {
        Start();

        long lastTick = Environment.TickCount64;

        while (_isRunning)
        {
            long currentTick = Environment.TickCount64;
            float delta = (currentTick - lastTick) / 1000f;
            lastTick = currentTick;

            _networkManager.Update();
            _lobbyManager.Update(delta);
            _combatManager.Update(delta);

            await Task.Delay(16); // ~60 Hz tick
        }

        _networkManager.Stop();
    }

    public void Stop()
    {
        Console.WriteLine("Arresto del server...");
        _isRunning = false;
    }
}
