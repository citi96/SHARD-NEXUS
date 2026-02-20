using System;

namespace Server
{
    public class GameServer
    {
        private MatchManager _matchManager;

        public GameServer()
        {
            _matchManager = new MatchManager();
        }

        public void Start()
        {
            Console.WriteLine("Server avviato. In attesa di connessioni...");
            // Inizializzazione della rete e della logica di base qui
        }

        public void Stop()
        {
            Console.WriteLine("Server arrestato.");
        }
    }
}
