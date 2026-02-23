using Server.Network;
using Server.GameLogic;

namespace Server;

public class GameServer
{
    private MatchManager _matchManager;
    private ServerNetworkManager _networkManager;
    private LobbyManager _lobbyManager;
    private bool _isRunning;

    public GameServer(int maxPlayers, int port, int ackTimeoutMs, int ackMaxRetries)
    {
        _matchManager = new MatchManager();
        _networkManager = new ServerNetworkManager(maxPlayers, port, ackTimeoutMs, ackMaxRetries);
        _lobbyManager = new LobbyManager(_networkManager, maxPlayers);
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
