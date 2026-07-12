using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Shared.Protocols;
using Messaging.Shared.UserIdentifiers;

namespace Messaging.Server;

public class MessageServer {

    public int Port { get; set; }

    private readonly IMessageProtocol protocol;

    private readonly TcpListener listener;

    private readonly Dictionary<MessageConnection, StringIdentifier> users;
    private readonly Dictionary<IPAddress, MessageConnection> connections;
    private readonly Dictionary<MessageConnection, MessageDataBuffer> outgoingBuffers;

    private readonly List<Task> tasks;

    public MessageServer(int port, IMessageProtocol protocol) {
        Port = port;
        this.protocol = protocol;
        listener = new(IPAddress.Any, Port);
        users = [ ];
        connections = [ ];
        tasks = [ ];
        outgoingBuffers = [ ];
    }

    public async Task RunAsync(CancellationToken ct) {
        listener.Start();

        while (!ct.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) {
                Console.WriteLine("TCP listening cancelled");
                break;
            }

            tasks.Add(HandleConnectionAsync(client, ct));
            
        }

        listener.Stop();
        listener.Dispose();

        foreach (Task task in tasks) await task;

    }

    public async Task HandleConnectionAsync(TcpClient client, CancellationToken ct) {
        MessageConnection conn = new(client, ct);
        Task task;
        task = conn.StartAsync();





        await task;
    }

}