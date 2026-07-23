namespace Messaging.Shared.Models;

public enum MessageState {
    Unsent, // target is offline
    Timeout, // ack waiting timeout, retry necessary
    Sent,
    Pending,
    AutoPending
}