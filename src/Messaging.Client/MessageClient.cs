using System.Net;
using System.Net.Sockets;

using Messaging.Shared.Models;
using Messaging.Client.Protocols;
using Messaging.Client.Services;
using Messaging.Shared.Services;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Messaging.Client;

public class MessageClient {

    private readonly IPAddress address;
    private readonly int port;
    private IClientMessageProtocol? protocol;

    private readonly IClientMessageProtocolFactory factory;

    private readonly TcpClient client;

    private MessageConnection? conn;
    private MessageConnectionHandler? handler;
    private readonly string username;

    public ClientDbHandler DbHandler { get; }
    private AckWaitHandler? ackHandler;

    public MessageClient(IPAddress address, int port, IClientMessageProtocolFactory factory, string username) {
        this.address = address;
        this.port = port;
        this.factory = factory;
        client = new(AddressFamily.InterNetworkV6);
        this.username = username;
        DbHandler = new();
    }

    // Connects and introduces to server, then starts listening for incoming and outgoing messages
    public async Task RunAsync(CancellationTokenSource cts) {
        CancellationToken ct = cts.Token;
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        StringIdentifier selfId = new(username);

        // Connect
        try {
            Console.WriteLine("Attempting connection");
            await client.ConnectAsync(address, port);
        }
        catch (Exception e) {
            Console.WriteLine($"Connection failed: {e.Message}");
            client.Dispose();
            return;
        }

        Console.WriteLine("Connection successful");
        conn = new(client, linked.Token);

        // Start the connection's incoming listener
        Task connTask = conn.StartAsync();

        handler = new(conn, linked);
        ackHandler = new(handler, true, linked.Token);
        protocol = factory.CreateProtocol(selfId, handler, DbHandler, ackHandler);

        // Send intro

        try {
            await conn.WriteAsync(protocol.CreateIntroduction());
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Introduction cancelled");
        }

        //Cancel waiting for ack after 5 seconds
        CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(5000);

        bool response = false;

        // wait for ack
        try {
            response = await handler.WaitForIncomingAsync(introCts.Token);
        }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested) {
            Console.WriteLine("Problem while waiting to receive ack");
        }

        if (!response) {
            Console.WriteLine("Timeout waiting for ack");
            linked.Cancel();
            await connTask;
            return;
        }
        
        
        bool introduced;

        //Check if message is actually ack
        try {
            introduced = (await handler.ReadOneIncomingAsync(linked.Token)).Type == MessageType.Ack;
        }
        catch (OperationCanceledException) {
            Console.WriteLine("ACK Read cancelled");
            introduced = false;
        }


        if (introduced) {
            Console.WriteLine("ACK Received");
        }
        else {
            Console.WriteLine("Unsuccessful introduction");
            linked.Cancel();
            await connTask;
            return;
        }

        // Start listening for incoming and outgoing messages
        Task handlerTask = handler.StartProcessingAsync(protocol);
        await SendUnsentMessagesAsync();

        await handlerTask;
        await connTask;
        cts.Cancel();
    }

    // Queue a text message to the outgoing buffer
    public async Task SendTextMessageAsync(string target, string text) {
        if (protocol is null) return;
        await protocol.SendTextMessageAsync(new StringIdentifier(target), text);
    }

    private async Task SendUnsentMessagesAsync() {
        MessageWrapper[] wrappers = await DbHandler.GetMessagesWithStateAsync(MessageState.Unsent);

        List<Task<bool>> sendTasks = [ ];
        List<MessageWrapper> realPendingMessages = [ ];

        foreach (var wrapper in wrappers) {
            wrapper.State = MessageState.Pending;

            try {
                MessageData? messageData = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                if (messageData is not null && ackHandler is not null) {
                    sendTasks.Add(ackHandler.EnqueueMessageAsync(messageData));
                    Console.WriteLine("Pending message enqueued");
                    realPendingMessages.Add(wrapper);
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Error delivering pending message to: {e.Message}");
            }
        }

        bool[] results = await Task.WhenAll(sendTasks);
        int success = 0, failure = 0;

        for (int i = 0; i < sendTasks.Count; ++i) {
            if (results[i]) {
                ++success;
            }
            else {
                ++failure;
            }
            await DbHandler.UpdateMessageStateAsync(realPendingMessages[i].Id, results[i] ? MessageState.Sent : MessageState.Unsent);
        }

        if (realPendingMessages.Count > 0) {
            Console.WriteLine($"Delivered {success} pending messages, failed {failure}");
        }
        


    }

}