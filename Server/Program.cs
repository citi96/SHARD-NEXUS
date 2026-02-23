namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Avvio del server SHARD NEXUS...");
        GameServer server = new GameServer();

        // Graceful shutdown via Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Server] Shutdown richiesto (Ctrl+C)...");
            server.Stop();
        };

        await server.RunAsync();
        Console.WriteLine("[Server] Server terminato.");
    }
}
