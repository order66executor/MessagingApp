namespace Messaging.Shared.Models;

public enum MessageType {
    Login,
    Register,
    Ack,
    Nack,
    TextMessage,
    FileUpload,
    FileNotification,
    FileRequest,
    FileResponse
}