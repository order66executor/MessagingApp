using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Messaging.Server.Protocols;
using Messaging.Shared.Models;
namespace Messaging.Server;

public class Program {

    private static MessageServer? server;

    private static readonly CancellationTokenSource cts = new();


    private static async Task Main(string[] args) {

        if (args.Length < 1) {
            Console.WriteLine("At least 1 parameter required for port number");
            return;
        }

        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) => cts.Cancel();

        PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                Console.WriteLine("SIGTERM received");
                Console.Out.Flush();
                cts.Cancel();
                context.Cancel = true;
            });

        PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            context =>
            {
                Console.WriteLine("SIGINT received");
                cts.Cancel();
                context.Cancel = true;
            });

        if (int.TryParse(args[0], out int port)) {

            bool tls = !args.Contains("--notls");

            server = new MessageServer(port, new ServerProtocolFactory(), tls, cts.Token);
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

        await serverTask;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); 


        Console.WriteLine("Exiting gracefully");
        Console.Out.Flush();


    }
}

