using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text.Json;
using System.Net.Security;

namespace Messaging.Shared.Models;

public class MessageConnection {
    private static readonly short sizeByteCount = 4;
    private readonly TcpClient client;
    public Stream Stream { get; }
    private readonly CancellationToken ct;
    private readonly bool useTls;

    //Buffer where incoming MessageData is placed
    public MessageDataBuffer Buffer { get; }

    public MessageConnection(TcpClient client, bool useTls, CancellationToken ct) {
        this.client = client;
        this.useTls = useTls;
        Buffer = new();

        Stream = !useTls ? client.GetStream() : new SslStream(client.GetStream(), leaveInnerStreamOpen: false);


        this.ct = ct;

        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
        client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
    }


    public async Task StartAsync() {
        try {

            // Listen forever for incoming messages
            while (true) {
                byte[] sizeBuffer = new byte[sizeByteCount];

                // wait until a new message is received then read the size into sizeBuffer
                try {
                    await Stream.ReadExactlyAsync(sizeBuffer, ct);
                }
                catch (Exception e) {
                    Console.WriteLine($"Listening for incoming tcp packets aborted: {e.Message}");
                    return;
                }

                // convert bytes to int
                int size = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);

                if (size is > 20_971_520 or < 0) {
                    Console.WriteLine($"Size is over 20MB or less than 0, it is: {size}");
                    throw new Exception();
                }

                byte[] payloadBuffer = new byte[size];

                // read message content
                await Stream.ReadExactlyAsync(payloadBuffer, ct);

                MessageData? data;

                // deserialize from bytes

                try {
                    data = JsonSerializer.Deserialize<MessageData>(payloadBuffer);
                }
                catch (Exception e) {
                    Console.WriteLine($"Deserialization failed: {e.Message}" );
                    continue;
                }

                if (data is null) continue;

                // write deserialized object into incoming buffer
                await Buffer.Writer.WriteAsync(data);

            }
        }
        finally {
            client.Close();
            client.Dispose();

            Buffer.Dispose();
        }

    }

    // Writes data to the stream
    public async Task WriteAsync(MessageData data) {
        // serialize message

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data);
        int size = payload.Length;

        // write size
        byte[] sizeAsBytes = new byte[sizeByteCount];
        BinaryPrimitives.WriteInt32BigEndian(sizeAsBytes, size);

        try {
            // send size then payload
            await Stream.WriteAsync(sizeAsBytes, ct);
            await Stream.WriteAsync(payload, ct);
        }
        catch (OperationCanceledException e) {
            Console.WriteLine($"The operation was cancelled: {e.Message}");
        }


    }




}