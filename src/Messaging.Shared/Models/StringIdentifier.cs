namespace Messaging.Shared.Models;

public readonly record struct StringIdentifier(string Value) {
    public override string ToString() => Value;

}