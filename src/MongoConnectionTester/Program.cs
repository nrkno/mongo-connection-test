using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoConnectionTester.Events;
using MongoDB.Driver;

namespace MongoConnectionTester;

internal class Program
{
    public static async Task<int> Main(string[] argz)
    {
        try
        {
            Console.WriteLine("Starting");
            Console.WriteLine("ctrl+c to quit");
            Console.WriteLine();
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) =>
            {
                Console.WriteLine("ctrl+c pressed");
                cts.Cancel();
                e.Cancel = true;
            };

            var cancellationToken = cts.Token;
            using var cluster = await DoRunAsync(cancellationToken);

            return 0;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Cancelled.");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }

    private static async Task<IDisposable> DoRunAsync(CancellationToken cancellationToken)
    {
        var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Debug);
        });

        using var monitor = new MongoClusterMonitor(loggerFactory.CreateLogger(nameof(MongoClusterMonitor)));
        var appSettings = GetAppSettings();
        var settings = GetMongoClientSettings(appSettings, monitor);
        var client = new MongoClient(settings);
        
        // List databases, just to make sure we can reach the mongo cluster.
        var databases = await client.ListDatabaseNamesAsync(cancellationToken);
        while (await databases.MoveNextAsync(cancellationToken))
        {
            foreach (var database in databases.Current)
            {
                Console.WriteLine($"{database}");
            }    
        }
        Console.WriteLine(ListServers(client));
        
        // Idle until ctrl+c
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);
        }

        return client.Cluster;
    }

    private static MongoClientSettings GetMongoClientSettings(AppSettings appSettings, MongoClusterMonitor monitor)
    {
        using var certificate = new X509Certificate2(appSettings.MongoDb.Certificate.PfxName, appSettings.MongoDb.Certificate.PfxPassword, X509KeyStorageFlags.Exportable);

        var settings = MongoClientSettings.FromConnectionString(appSettings.MongoDb.ConnectionString);

        settings.UseTls = true;
        settings.SslSettings.ClientCertificates = new[] {certificate};
        settings.SslSettings.ClientCertificateSelectionCallback = (_, _, _, _, _) => certificate;
        settings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12;
        settings.MinConnectionPoolSize = 100;
        settings.MaxConnectionPoolSize = 200;
        settings.MaxConnectionIdleTime = TimeSpan.FromSeconds(15);
        settings.ClusterConfigurator = b =>
        {
            b.Subscribe(monitor);
            b.ConfigureConnectionPool(s => s.With(maintenanceInterval: TimeSpan.FromSeconds(10)));
        };

        Console.WriteLine(FormatSettings(settings));
        return settings;
    }

    private static AppSettings GetAppSettings()
    {
        var appSettings = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.local.json")
            .Build()
            .Get<AppSettings>();

        if (string.IsNullOrWhiteSpace(appSettings.MongoDb.ConnectionString) || string.IsNullOrWhiteSpace(appSettings.MongoDb.Certificate.PfxName))
        {
            throw new ArgumentException("ConnectionString and certificate must be configured. See README.md");
        }

        return appSettings;
    }

    private static string ListServers(IMongoClient client)
    {
        var builder = new StringBuilder("Client:").AppendLine();
        builder.AppendLine("Servers:");
        foreach (var server in client.Cluster.Description.Servers)
        {
            builder.AppendLine($"  {server.EndPoint}: {server.State}");
        }
       
        return builder.ToString();
    }

    private static string FormatSettings(MongoClientSettings settings)
    {
        var builder = new StringBuilder("Settings:").AppendLine();
        foreach (var property in settings.GetType().GetProperties().Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)))
        {
            builder.AppendLine($"  {property.Name}: '{property.GetValue(settings)}'");
        }

        builder.AppendLine();
        
        return builder.ToString();
    }
}