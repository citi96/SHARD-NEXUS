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
        int port = config.GetValue<int>("GameSettings:Port", 7777);
        int ackTimeoutMs = config.GetValue<int>("GameSettings:AckTimeoutMs", 500);
        int ackMaxRetries = config.GetValue<int>("GameSettings:AckMaxRetries", 3);
        
        var echoPoolSettings = config.GetSection("EchoPoolSettings").Get<Server.Configuration.EchoPoolSettings>() ?? new Server.Configuration.EchoPoolSettings();
        var shopSettings = config.GetSection("ShopSettings").Get<Server.Configuration.ShopSettings>() ?? new Server.Configuration.ShopSettings();

        Console.WriteLine($"Avvio del server SHARD NEXUS (Port: {port}, Max Players: {maxPlayers})...");
        GameServer server = new GameServer(maxPlayers, port, ackTimeoutMs, ackMaxRetries, echoPoolSettings, shopSettings);

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
