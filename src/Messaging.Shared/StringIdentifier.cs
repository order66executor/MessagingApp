namespace Messaging.Shared.UserIdentifiers;
using System.Text;

public class StringIdentifier {

    public string Value { get; }
    private static readonly UTF8Encoding utf8 = new(false, true);
    public StringIdentifier(string? id) {
        Value = id ?? throw new ArgumentException(nameof(id));
    }

    public bool Equals(StringIdentifier? other) {
        return other is not null && Value == other.Value;
    }

    // override object.Equals
    public override bool Equals(object? obj)
    {
        return obj is StringIdentifier other && Equals(other);
    }
    
    // override object.GetHashCode
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }


    public static bool operator ==(StringIdentifier left, StringIdentifier right) => left.Equals(right);
    public static bool operator !=(StringIdentifier left, StringIdentifier right) => !left.Equals(right);

    public static bool TryFromBytes(byte[] bytes, out StringIdentifier buffer) {
        string str;

        try {
            str = utf8.GetString(bytes);
        }
        catch (Exception) {
            buffer = new StringIdentifier("");
            return false;
        }

        buffer = new StringIdentifier(str);
        return true;
    }            

    public byte[] ToBytes() {
        return utf8.GetBytes(Value);
    }

}