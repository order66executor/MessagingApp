namespace Messaging.Server.Models;

public enum MessageState {
    Unsent, // target is offline
    Waiting, // waiting for ack from target
    Timeout, // ack waiting timeout, retry necessary
    Sent
}