using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public readonly record struct StringIdentifier([property: Key(0)] string Value) {
    public override string ToString() => Value;

}