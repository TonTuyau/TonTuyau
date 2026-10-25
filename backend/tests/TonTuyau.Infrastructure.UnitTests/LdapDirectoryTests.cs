using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.UnitTests;

/// <summary>What the LDAP directory sends (2.3.0): a name escaped into a filter, a Password Modify request.</summary>
public sealed class LdapDirectoryTests
{
    [Theory]
    [InlineData("marie", "marie")]
    [InlineData("*", @"\2a")]
    [InlineData("a)(uid=*", @"a\29\28uid=\2a")]
    [InlineData(@"back\slash", @"back\5cslash")]
    [InlineData("nul\0", @"nul\00")]
    public void A_name_is_escaped_as_RFC_4515_says_no_one_widens_the_filter(string name, string escaped)
    {
        Assert.Equal(escaped, NovellLdapDirectory.Escape(name));
    }

    [Fact]
    public void A_Password_Modify_request_is_its_identity_the_old_password_when_given_and_the_new_one()
    {
        byte[] request = NovellLdapDirectory.PasswordModifyRequest("uid=a", "old", "new");

        Assert.Equal(
            [0x30, 17, 0x80, 5, .. "uid=a"u8, 0x81, 3, .. "old"u8, 0x82, 3, .. "new"u8],
            request);
        Assert.Equal(
            [0x30, 12, 0x80, 5, .. "uid=a"u8, 0x82, 3, .. "new"u8],
            NovellLdapDirectory.PasswordModifyRequest("uid=a", null, "new"));
    }

    [Fact]
    public void A_long_value_takes_the_long_form_length()
    {
        string password = new('p', 200);

        byte[] request = NovellLdapDirectory.PasswordModifyRequest("uid=a", null, password);

        Assert.Equal([0x30, 0x81, 7 + 3 + 200], request[..3]);
        Assert.Equal([0x82, 0x81, 200], request[10..13]);
    }
}
