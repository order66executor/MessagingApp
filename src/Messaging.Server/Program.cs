using System;
using System.Net.Sockets;
using Messaging.Shared;
using Messaging.Shared.Protocols;
namespace Messaging.Server;

public class Program {

    private static MessageServer? server;

    private static readonly CancellationTokenSource cts = new();


    private static async Task Main(string[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Exactly 1 parameter required for port number");
            return;
        }

        if (int.TryParse(args[0], out int port)) {
            server = new MessageServer(port, new StandardProtocol());
        }
        else {
            Console.WriteLine("Invalid port number");
            return;
        }

        Task serverTask = server.RunAsync(cts.Token);

        while (true) {
            string? input = Console.ReadLine();

            if (input is null) continue;

            if (input == "quit") {
                Console.WriteLine("Cancel request received");
                cts.Cancel();
                break;
            }

        }

        await serverTask;

        cts.Dispose();

    }
}

