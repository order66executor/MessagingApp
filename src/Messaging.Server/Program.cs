using System;
using System.Net.Sockets;
using Messaging.Shared;
using Messaging.Shared.Protocols;
namespace Messaging.Server;

public class Program {

    private static MessageServer? _server;

    private static readonly CancellationTokenSource cts = new();


    private static async Task Main(String[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Exactly 1 parameter required for port number");
            return;
        }

        if (int.TryParse(args[0], out int port)) {
            _server = new MessageServer(port, new StandardProtocol());
        }
        else {
            Console.WriteLine("Invalid port number");
            return;
        }

        Task serverTask = _server.RunAsync(cts.Token);

        while (true) {
            string? input = Console.ReadLine();

            if (input is null) continue;

            if (input == "quit") {
                cts.Cancel();
                break;
            }

        }

        await serverTask;

        cts.Dispose();

    }
}

