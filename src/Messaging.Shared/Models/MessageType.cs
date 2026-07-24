namespace Messaging.Shared.Models;

public enum MessageType {
    Introduction,
    Ack,
    TextMessage,
    FileUpload,
    FileNotification,
    FileRequest,
    FileResponse
}