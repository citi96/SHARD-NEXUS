using Server.Network;

namespace Server
{
    public class GameServer
    {
        private MatchManager _matchManager;
        private ServerNetworkManager _networkManager;

        public GameServer()
        {
            _matchManager = new MatchManager();
            _networkManager = new ServerNetworkManager();
        }

        public void Start()
        {
            Console.WriteLine("Server avviato. Inizializzazione moduli in corso...");
            _networkManager.Start();
        }

        public void Stop()
        {
            Console.WriteLine("Arresto del server...");
            _networkManager.Stop();
        }
    }
}
