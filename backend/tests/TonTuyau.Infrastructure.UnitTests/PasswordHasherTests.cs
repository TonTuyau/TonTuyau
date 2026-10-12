using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class PasswordHasherTests
{
    private static readonly Argon2idPasswordHasher PasswordHasher = new();

    [Fact]
    public void A_hash_verifies_its_password()
    {
        string phc = PasswordHasher.Hash("correct horse battery staple");

        Assert.True(PasswordHasher.Verify("correct horse battery staple", phc));
    }

    [Theory]
    [InlineData("correct horse battery stapl")]
    [InlineData("Correct horse battery staple")]
    [InlineData("")]
    public void A_hash_refuses_any_other_password(string other)
    {
        string phc = PasswordHasher.Hash("correct horse battery staple");

        Assert.False(PasswordHasher.Verify(other, phc));
    }

    [Fact]
    public void The_hash_is_a_phc_string_with_owasp_parameters()
    {
        string phc = PasswordHasher.Hash("pässwörd");

        // 16 bytes of salt and 32 of hash, unpadded Base64: 22 and 43 characters.
        Assert.Matches(@"\A\$argon2id\$v=19\$m=19456,t=2,p=1\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}\z", phc);
    }

    [Fact]
    public void Every_hash_has_its_own_salt()
    {
        string first = PasswordHasher.Hash("same password");
        string second = PasswordHasher.Hash("same password");

        Assert.NotEqual(first.Split('$')[4], second.Split('$')[4]);
        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("same password", first));
        Assert.True(PasswordHasher.Verify("same password", second));
    }

    /// <summary>
    /// Argon2's reference implementation (phc-winner-argon2, test.c): the parameters come from the string, so hashes
    /// made with other parameters (raised later, or by another tool) still verify.
    /// </summary>
    [Fact]
    public void A_hash_from_the_reference_implementation_verifies()
    {
        const string reference =
            "$argon2id$v=19$m=65536,t=2,p=1$c29tZXNhbHQ$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc";

        Assert.True(PasswordHasher.Verify("password", reference));
        Assert.False(PasswordHasher.Verify("passwore", reference));
    }

    [Theory]
    [InlineData("")]
    [InlineData("password")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c29tZXNhbHQ$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc")]
    [InlineData("$argon2id$v=16$m=19456,t=2,p=1$c29tZXNhbHQ$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc")]
    [InlineData("$argon2id$v=19$m=19456,t=0,p=1$c29tZXNhbHQ$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc")]
    [InlineData("$argon2id$v=19$t=2,m=19456,p=1$c29tZXNhbHQ$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$not*base64$CTFhFdXPJO1aFaMaO6Mm5c8y7cJHAph8ArZWb2GRPPc")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$$")]
    public void A_malformed_hash_verifies_nothing(string phc)
    {
        Assert.False(PasswordHasher.Verify("password", phc));
    }
}
