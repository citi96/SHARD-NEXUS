using System;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Avvio del server SHARD NEXUS...");
            GameServer server = new GameServer();
            server.Start();
            
            // Mantieni attivo il server
            Console.ReadLine();
        }
    }
}
