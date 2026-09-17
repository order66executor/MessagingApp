using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

using MessagePack;

namespace Messaging.Client.Protocols.Handlers;

public class FileResponseHandler : IMessageHandler {
    public MessageType SupportedType { get; } = MessageType.FileResponse;

    private readonly MessageConnectionHandler connHandler;

    public FileResponseHandler(MessageConnectionHandler connHandler) {
        this.connHandler = connHandler;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        var resPayload = MessagePackSerializer.Deserialize<FileResponsePayload>(message.Payload);
        if (resPayload != null) {
            string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string savePath = Path.Combine(downloadsDir, resPayload.FileName);
            await File.WriteAllBytesAsync(savePath, resPayload.FileData);
            Console.WriteLine($"File downloaded and saved to: {savePath}");
            // Here we might want to trigger a local UI event
        }
        await connHandler.WriteToOutBufferAsync(AckFactory.CreateAck(message.TargetId, message.SourceId, message.Id));
        return true;
    }


}

