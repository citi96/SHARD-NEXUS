using Server.Network;
using Server.GameLogic;
using Server.Configuration;
using Shared.Models.Structs;
using Shared.Models.Enums;
using System.Collections.Generic;

namespace Server;

public class GameServer
{
    private MatchManager _matchManager;
    private ServerNetworkManager _networkManager;
    private LobbyManager _lobbyManager;
    private EchoPoolManager _echoPoolManager;
    private bool _isRunning;

    public GameServer(int maxPlayers, int port, int ackTimeoutMs, int ackMaxRetries, EchoPoolSettings echoPoolSettings)
    {
        _matchManager = new MatchManager();
        _networkManager = new ServerNetworkManager(maxPlayers, port, ackTimeoutMs, ackMaxRetries);
        _lobbyManager = new LobbyManager(_networkManager, maxPlayers);

        // Mock Catalog for Echo Pool initialization
        var mockCatalog = new List<EchoDefinition>
        {
            new EchoDefinition(1, "Pyroth", Rarity.Common, EchoClass.Vanguard, Resonance.Fire, 500, 100, 50, 20, new int[]{}),
            new EchoDefinition(2, "Aquos", Rarity.Uncommon, EchoClass.Caster, Resonance.Frost, 300, 200, 70, 15, new int[]{}),
            new EchoDefinition(3, "Terron", Rarity.Rare, EchoClass.Vanguard, Resonance.Earth, 800, 50, 30, 60, new int[]{}),
            new EchoDefinition(4, "Zephyr", Rarity.Epic, EchoClass.Assassin, Resonance.Lightning, 400, 150, 90, 10, new int[]{}),
            new EchoDefinition(5, "Lumin", Rarity.Legendary, EchoClass.Support, Resonance.Light, 600, 300, 40, 40, new int[]{})
        };

        _echoPoolManager = new EchoPoolManager(echoPoolSettings, mockCatalog);
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
