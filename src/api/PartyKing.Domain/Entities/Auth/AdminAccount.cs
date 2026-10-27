namespace PartyKing.Domain.Entities.Auth;

public class AdminAccount
{
    private AdminAccount()
    {
    }

    public AdminAccount(string username, string passwordHash)
    {
        Id = Guid.NewGuid();
        Username = username;
        PasswordHash = passwordHash;
    }

    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
}
