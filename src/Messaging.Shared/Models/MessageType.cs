namespace Messaging.Shared.Models;

// Types that a MessageData can be
public enum MessageType {
    Login,
    Register,
    Ack,
    Nack,
    TextMessage,
    FileUpload,
    FileNotification,
    FileRequest,
    FileResponse,
    FileTransferReady,
    Segment
}