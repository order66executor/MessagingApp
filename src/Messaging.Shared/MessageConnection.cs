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
    }

    public async Task StartAsync() {
        while (!ct.IsCancellationRequested) {
            byte[] sizeBuffer = new byte[sizeByteCount];

            await stream.ReadExactlyAsync(sizeBuffer, ct);

            int size = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);
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

            Buffer.Writer.TryWrite(data);

        }

        client.Close();
        client.Dispose();

        Buffer.Dispose();

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