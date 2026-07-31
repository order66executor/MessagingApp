using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

using System.Collections.Concurrent;
using System.Text;
using MessagePack;
using Messaging.Server.Services;

namespace Messaging.Server.Protocols;

public class ServerProtocol : ProtocolBase, IServerMessageProtocol {

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly MessageRouter router;

    public ServerProtocol(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, MessageRouter router) {
        this.handlers = handlers;
        this.router = router;
    }

    public StringIdentifier ReceiveIntroduction(MessageData message) => message.SourceId;

    public sealed override async Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message) {
        if (sourceId != message.SourceId) return false;
        bool result;

        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK received");
                router.AckHandler.SubmitAck(message);
                return true;

            case MessageType.FileUpload:
                if (handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? uploadHandler)) {
                    var uploadPayload = MessagePackSerializer.Deserialize<FileUploadPayload>(message.Payload);
                    if (uploadPayload != null) {
                        string fileId = Guid.NewGuid().ToString();
                        string saveDir = Path.Combine(Environment.CurrentDirectory, "FileStorage");
                        Directory.CreateDirectory(saveDir);
                        string sanitizedFileName = Path.GetFileName(uploadPayload.FileName);
                        string savePath = Path.Combine(saveDir, $"{fileId}_{sanitizedFileName}");
                        
                        await File.WriteAllBytesAsync(savePath, uploadPayload.FileData);
                        
                        // Send Notification to the receiver
                        var notifPayload = new FileNotificationPayload { FileId = fileId, FileName = sanitizedFileName, FileSize = uploadPayload.FileData.Length };
                        MessageData notifMsg = new() { Id = message.Id, Type = MessageType.FileNotification, SourceId = message.SourceId, TargetId = message.TargetId, SentAtUtc = DateTime.UtcNow, Payload = MessagePackSerializer.Serialize(notifPayload) };
                        _ = router.RouteMessageAsync(notifMsg);
                    }
                    try {
                        await EnqueueAck(uploadHandler, new("SYSTEM"), message.TargetId, message.Id);
                    } catch (Exception e) {
                        Console.WriteLine($"Exception thrown while replying Ack to sender: {e.Message}");
                    }
                }
                return true;

            case MessageType.FileRequest:
                Console.WriteLine("File request received");
                if (handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? requestHandler)) {
                    try {
                        await EnqueueAck(requestHandler, message.SourceId, new("SYSTEM"), message.Id);
                    } catch (Exception e) {
                        Console.WriteLine($"Exception thrown while replying Ack to sender: {e.Message}");
                    }
                    var reqPayload = MessagePackSerializer.Deserialize<FileRequestPayload>(message.Payload);
                    if (reqPayload != null) {
                        string saveDir = Path.Combine(Environment.CurrentDirectory, "FileStorage");
                        var filePath = Directory.GetFiles(saveDir, $"{reqPayload.FileId}_*").FirstOrDefault();
                        if (filePath != null) {
                            byte[] fileData = await File.ReadAllBytesAsync(filePath);
                            string fileName = Path.GetFileName(filePath)[(reqPayload.FileId.Length + 1)..];
                            
                            var resPayload = new FileResponsePayload { FileId = reqPayload.FileId, FileName = fileName, FileData = fileData };
                            MessageData resMsg = new() { Id = message.Id, Type = MessageType.FileResponse, SourceId = new("SYSTEM"), TargetId = message.SourceId, SentAtUtc = DateTime.UtcNow, Payload = MessagePackSerializer.Serialize(resPayload) };
                            _ = router.RouteMessageAsync(resMsg);
                        }
                    }
                }
                return true;
            
            default:
                Console.WriteLine($"Message with ID: {message.Id} received from: {message.SourceId} to: {message.TargetId} sent at: {message.SentAtUtc}");
                if (handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler)) {
                    try {
                        await EnqueueAck(handler, new("SYSTEM"), message.TargetId, message.Id);

                    }
                    catch (Exception e) {
                        Console.WriteLine($"Exception thrown while replying Ack to sender: {e.Message}");
                    }
                }
                else {
                    Console.WriteLine("Sender disconnected before Ack could be sent");
                }

                result = await router.UpdateHighestAckAsync(message);

                if (result)
                    _ = router.RouteMessageAsync(message);
                else Console.WriteLine("Message already acked, discarding");
                return true;
            
        }
    }


}