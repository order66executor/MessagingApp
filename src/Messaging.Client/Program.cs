using System.Net;

using Messaging.Shared.Protocols;

namespace Messaging.Client;

public class Program {

    private static MessageClient? client;

    private static readonly CancellationTokenSource cts = new();
    public static async Task Main(string[] args) {
        if (args.Length != 2) {
            Console.WriteLine("Exactly 2 parameters required");
            return;
        }

        if (int.TryParse(args[1], out int port) && IPAddress.TryParse(args[0], out IPAddress? address)) {
            Console.Write("Enter username: ");
            string? username;

            while ((username = Console.ReadLine()) is null);
            client = new(address, port, new StandardProtocol(), username);
        }
        else {
            Console.WriteLine("Invalid port number or address");
            return;
        }
        Console.WriteLine("Starting client");
        Task clientTask = client.RunAsync(cts.Token);
        Console.WriteLine("Client started");  

        while (!cts.IsCancellationRequested) {
            string? input = Console.ReadLine();

            if (input is null) continue;

            if (input == "quit") {
                Console.WriteLine("Cancel request received");
                cts.Cancel();
            }

        }

        await clientTask;
        cts.Dispose();

    }


}