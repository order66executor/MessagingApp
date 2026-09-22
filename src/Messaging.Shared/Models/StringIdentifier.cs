using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
// Wraps a string and acts as a user identifier. Currently this is pointless but we might 
// want to replace this someday with a different kind of id
public readonly record struct StringIdentifier([property: Key(0)] string Value) {
    public override string ToString() => Value;
    public static StringIdentifier System { get; } = new("SYSTEM");
}