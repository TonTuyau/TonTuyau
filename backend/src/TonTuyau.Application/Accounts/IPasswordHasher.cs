namespace TonTuyau.Application.Accounts;

/// <summary>Turns a password into what's stored, and checks a password against it.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>True when <paramref name="password"/> is the one <paramref name="hash"/> was made from.</summary>
    bool Verify(string password, string hash);
}
