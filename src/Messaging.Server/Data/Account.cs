namespace Messaging.Server.Data;

public class Account {

    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }

}