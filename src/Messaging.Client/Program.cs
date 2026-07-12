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
            client = new(address, port, new StandardProtocol());
        }
        else {
            Console.WriteLine("Invalid port number or address");
            return;
        }

        Task clientTask = client.RunAsync(cts.Token);

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