using Microsoft.Extensions.Configuration;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        int maxPlayers = config.GetValue<int>("GameSettings:MaxPlayers", 2);

        Console.WriteLine($"Avvio del server SHARD NEXUS (Max Players: {maxPlayers})...");
        GameServer server = new GameServer(maxPlayers);

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
