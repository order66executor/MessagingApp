using System.Runtime.InteropServices;

namespace Messaging.Server;

public class Program {

    private static MessageServer? server;

    private static readonly CancellationTokenSource cts = new();

    // args[0] = port number
    private static async Task Main(string[] args) {

        if (args.Length < 1) {
            Console.WriteLine("At least 1 parameter required for port number");
            return;
        }

        // Handle cancel key
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) => cts.Cancel();

        // Handle POSIX signals
        using var termSignalRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                Console.WriteLine("SIGTERM received");
                Console.Out.Flush();
                cts.Cancel();
                context.Cancel = true;
            });

        using var intSignalRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            context =>
            {
                Console.WriteLine("SIGINT received");
                cts.Cancel();
                context.Cancel = true;
            });

        if (int.TryParse(args[0], out int port)) {

            bool tls = !args.Contains("--notls");

            server = new MessageServer(port, tls, cts.Token);
        }
        else {
            Console.WriteLine("Invalid port number");
            return;
        }

        Task serverTask = server.RunAsync();

/*         while (true) {
            string? input = Console.ReadLine();

            if (input is null) continue;

            if (input == "quit") {
                Console.WriteLine("Cancel request received");
                cts.Cancel();
                break;
            }

        } */

        // Wait for server to exit
        await serverTask;

        // Make sure all databases are closed
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); 


        Console.WriteLine("Exiting gracefully");
        Console.Out.Flush();


    }
}

