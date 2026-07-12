namespace Messaging.Shared.UserIdentifiers;

public interface IUserIdentifier {

    IdentifierType Type { get; }
    byte[] ToBytes();
    string AsCanonicalString();

}