using System.Net;
using System.Text;
using System.Text.Json;

using Messaging.Client.Protocols;
using Messaging.Shared;
using Messaging.Shared.Models;

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
            client = new(address, port, new ClientProtocolFactory(), username);
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

            List<string> parsed = ParseString(input);

            switch (parsed[0]) {
                case "quit":
                    Console.WriteLine("Cancel request received");
                    cts.Cancel();
                    break;

                case "send":
                    await client.SendTextMessageAsync(parsed[1], parsed[2]);
                    break;

                case "read":
                    foreach (MessageWrapper wrapper in await client.DbHandler.GetMessagesAsync(new StringIdentifier(parsed[1]))) {
                        MessageData? message = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                        if (message is not null)
                            Console.WriteLine($"Message ID: {message.Id} sent by: {message.SourceId} at: {message.SentAtUtc}, content: {Encoding.UTF8.GetString(message.Payload)}");
                    }
                    break;

                default:
                    break;
                
            }

        }

        await clientTask;
        cts.Dispose();

    }

    // Parses input string into split parts. only for now. use [] instead of "" for multi-word messages
    static List<string> ParseString(string str) {
        List<string> ret = [ ];

        int brackets = 0;
        string curr = "";

        foreach (char c in str) {
            if (c != ' ' && c != '\n') {
                if (c == '[') {

                    brackets++;

                    if (brackets > 1) {
                        curr += c;
                    }
                }
                else if (c == ']') {

                    brackets--;

                    if (brackets == 0) {
                        ret.Add(curr);
                        curr = "";
                    }
                    else {
                        curr += c;
                    }
                }
                else {
                    curr += c;
                }
            }

            else {
                if (brackets > 0) {
                    curr += c;
                }
                else {
                    if (curr == "") continue;
                    ret.Add(curr);
                    curr = "";
                }
            }

        }

        if (curr != "") ret.Add(curr);

        return ret;

    }
}