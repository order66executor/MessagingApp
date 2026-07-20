using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text.Json;
using System.Reflection.Metadata;

namespace Messaging.Shared;

public class MessageConnection {
    private static readonly short sizeByteCount = 4;
    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly CancellationToken ct;

    public MessageDataBuffer Buffer { get; }

    public MessageConnection(TcpClient client, CancellationToken ct) {
        this.client = client;
        Buffer = new();
        stream = client.GetStream();
        this.ct = ct;

        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
    }

    public async Task StartAsync() {
        try {
            while (true) {
                byte[] sizeBuffer = new byte[sizeByteCount];

                try {
                    await stream.ReadExactlyAsync(sizeBuffer, ct);
                }
                catch (Exception e) {
                    Console.WriteLine($"Listening for incoming tcp packets aborted: {e.Message}");
                    return;
                }

                int size = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);

                if (size > 512) {
                    Console.WriteLine($"Size is over 512, it is: {size}");
                    throw new Exception();
                }

                byte[] payloadBuffer = new byte[size];

                await stream.ReadExactlyAsync(payloadBuffer, ct);

                MessageData? data;

                try {
                    data = JsonSerializer.Deserialize<MessageData>(payloadBuffer);
                }
                catch (Exception e) {
                    Console.WriteLine($"Deserialization failed: {e.Message}" );
                    continue;
                }

                if (data is null) continue;

                await Buffer.Writer.WriteAsync(data);

            }
        }
        finally {
            client.Close();
            client.Dispose();

            Buffer.Dispose();
        }

    }

    public async Task WriteAsync(MessageData data) {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data);
        int size = payload.Length;
        byte[] sizeAsBytes = new byte[sizeByteCount];
        BinaryPrimitives.WriteInt32BigEndian(sizeAsBytes, size);
        try {
            await stream.WriteAsync(sizeAsBytes, ct);
            await stream.WriteAsync(payload, ct);
        }
        catch (OperationCanceledException e) {
            Console.WriteLine($"The operation was cancelled: {e.Message}");
        }


    }




}