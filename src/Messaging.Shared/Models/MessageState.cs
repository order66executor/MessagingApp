namespace Messaging.Shared.Models;

// States that the message can be in inside the db
public enum MessageState {
    Unsent, // target is offline
    Timeout, // ack waiting timeout, retry necessary
    Sent,
    Pending,
    AutoPending
}