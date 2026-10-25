using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// The LDAP directory through Novell.Directory.Ldap.NETStandard (2.3.0): a connection per call (logins and checks are
/// few), 5 seconds to answer. A name is escaped into its filter (RFC 4515); an empty password never binds (an LDAP
/// server would take it as an anonymous bind). A password is changed with the Password Modify operation (RFC 3062) when
/// the directory has it (LLDAP, OpenLDAP), else by writing the password attribute.
/// </summary>
internal sealed partial class NovellLdapDirectory(ILogger<NovellLdapDirectory> logger) : ILdapDirectory
{
    /// <summary>RFC 3062's Password Modify extended operation.</summary>
    public const string PasswordModifyOid = "1.3.6.1.4.1.4203.1.11.1";

    private const int TimeoutMilliseconds = 5000;
    private const int MaxResults = 10_000;

    public async Task CheckAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        using LdapConnection connection = await ServiceAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LdapFound> FindAsync(
        LdapSettings settings,
        string filter,
        int max,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using LdapConnection connection = await ServiceAsync(settings, cancellationToken).ConfigureAwait(false);
        List<LdapPerson> people = await SearchAsync(connection, settings, filter, cancellationToken)
            .ConfigureAwait(false);
        return new LdapFound(
            people.Count,
            [.. people.OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase).Take(max)]);
    }

    public async Task<LdapPerson?> FindUserAsync(
        LdapSettings settings,
        string username,
        string? filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string byName = $"({settings.UidAttribute}={Escape(username)})";
        using LdapConnection connection = await ServiceAsync(settings, cancellationToken).ConfigureAwait(false);
        string search = filter is null ? byName : $"(&{byName}{filter})";
        List<LdapPerson> people = await SearchAsync(connection, settings, search, cancellationToken)
            .ConfigureAwait(false);
        return people.Count == 1 ? people[0] : null;
    }

    public async Task<bool> CheckPasswordAsync(
        LdapSettings settings,
        string dn,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        using LdapConnection connection = await ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.BindAsync(dn, password, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return false;
        }
        catch (LdapException ex)
        {
            throw Unavailable(settings, ex);
        }
    }

    public async Task ChangePasswordAsync(
        LdapSettings settings,
        string dn,
        string current,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? refusal;
        using (LdapConnection service = await ServiceAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            bool modify = await HasPasswordModifyAsync(service, cancellationToken).ConfigureAwait(false);
            refusal = await TryChangeAsync(service, settings, modify, dn, null, password, cancellationToken)
                .ConfigureAwait(false);
            if (refusal is null)
            {
                return;
            }

            LogRefusedToService(dn, refusal);
            // The service account may not change this one (LLDAP: a password manager, an LLDAP admin's): as itself.
            using LdapConnection own = await ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
            try
            {
                await own.BindAsync(dn, current, cancellationToken).ConfigureAwait(false);
            }
            catch (LdapException ex)
            {
                throw new LdapRefusedException(ex.LdapErrorMessage ?? ex.Message, ex);
            }

            refusal = await TryChangeAsync(own, settings, modify, dn, current, password, cancellationToken)
                .ConfigureAwait(false);
        }

        if (refusal is not null)
        {
            throw new LdapRefusedException(refusal);
        }
    }

    /// <returns>Null when changed; why the directory refused otherwise.</returns>
    private static async Task<string?> TryChangeAsync(
        LdapConnection connection,
        LdapSettings settings,
        bool passwordModify,
        string dn,
        string? current,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            if (passwordModify)
            {
                LdapExtendedResponse response = await connection
                    .ExtendedOperationAsync(
                        new LdapExtendedOperation(PasswordModifyOid, PasswordModifyRequest(dn, current, password)),
                        cancellationToken)
                    .ConfigureAwait(false);
                return response.ResultCode == LdapException.Success
                    ? null
                    : response.ErrorMessage ?? response.ResultCode.ToString(CultureInfo.InvariantCulture);
            }

            await connection
                .ModifyAsync(
                    dn,
                    new LdapModification(
                        LdapModification.Replace,
                        new LdapAttribute(settings.PasswordAttribute, password)),
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (LdapException ex) when (ex.ResultCode is not LdapException.ConnectError and not LdapException.ServerDown)
        {
            return ex.LdapErrorMessage is { Length: > 0 } message ? message : ex.Message;
        }
    }

    /// <summary>
    /// RFC 3062's request, in BER: SEQUENCE { userIdentity [0], oldPasswd [1] (when given), newPasswd [2] }. LLDAP wants
    /// the identity even for one's own.
    /// </summary>
    internal static byte[] PasswordModifyRequest(string dn, string? current, string password)
    {
        List<byte> fields = [];
        Field(fields, 0x80, dn);
        if (current is not null)
        {
            Field(fields, 0x81, current);
        }

        Field(fields, 0x82, password);
        return [0x30, .. Length(fields.Count), .. fields];
    }

    private static void Field(List<byte> into, byte tag, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        into.Add(tag);
        into.AddRange(Length(bytes.Length));
        into.AddRange(bytes);
    }

    private static byte[] Length(int length) => length switch
    {
        < 0x80 => [(byte)length],
        < 0x100 => [0x81, (byte)length],
        _ => [0x82, (byte)(length >> 8), (byte)length],
    };

    private static async Task<bool> HasPasswordModifyAsync(
        LdapConnection connection,
        CancellationToken cancellationToken)
    {
        LdapEntry root = await connection
            .ReadAsync("", ["supportedExtension"], cancellationToken)
            .ConfigureAwait(false);
        return root.GetAttributeSet().TryGetValue("supportedExtension", out LdapAttribute? extensions)
            && extensions.StringValueArray.Contains(PasswordModifyOid, StringComparer.Ordinal);
    }

    private static async Task<List<LdapPerson>> SearchAsync(
        LdapConnection connection,
        LdapSettings settings,
        string filter,
        CancellationToken cancellationToken)
    {
        LdapSearchConstraints constraints = new()
        {
            TimeLimit = TimeoutMilliseconds,
            MaxResults = MaxResults,
        };
        List<LdapPerson> people = [];
        try
        {
            ILdapSearchResults results = await connection
                .SearchAsync(
                    settings.BaseDn,
                    LdapConnection.ScopeSub,
                    filter,
                    [settings.UidAttribute, settings.UsernameAttribute],
                    typesOnly: false,
                    constraints,
                    cancellationToken)
                .ConfigureAwait(false);
            while (await results.HasMoreAsync(cancellationToken).ConfigureAwait(false))
            {
                LdapEntry entry = await results.NextAsync(cancellationToken).ConfigureAwait(false);
                LdapAttributeSet attributes = entry.GetAttributeSet();
                string? uid = attributes.TryGetValue(settings.UidAttribute, out LdapAttribute? id)
                    ? id.StringValue
                    : null;
                string? shown = attributes.TryGetValue(settings.UsernameAttribute, out LdapAttribute? name)
                    ? name.StringValue
                    : null;
                if (uid is not null)
                {
                    people.Add(new LdapPerson(entry.Dn, uid, shown));
                }
            }
        }
        catch (LdapException ex)
        {
            throw Unavailable(settings, ex);
        }

        return people;
    }

    /// <summary>Connected, and the service account logged in.</summary>
    private async Task<LdapConnection> ServiceAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        LdapConnection connection = await ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
        try
        {
            await connection
                .BindAsync(settings.BindDn, settings.BindPassword ?? "", cancellationToken)
                .ConfigureAwait(false);
            return connection;
        }
        catch (LdapException ex)
        {
            connection.Dispose();
            throw ex.ResultCode == LdapException.InvalidCredentials
                ? new LdapUnavailableException("the service account's DN or password was refused.", ex)
                : Unavailable(settings, ex);
        }
    }

    private async Task<LdapConnection> ConnectAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Uri url = new(settings.Url);
        bool ldaps = url.Scheme.Equals("ldaps", StringComparison.OrdinalIgnoreCase);
        int port = url.IsDefaultPort || url.Port < 0 ? (ldaps ? 636 : 389) : url.Port;
        LdapConnectionOptions options = new LdapConnectionOptions()
            .ConfigureRemoteCertificateValidationCallback((_, certificate, _, errors) =>
                errors == SslPolicyErrors.None || TrustedBy(settings.CaCertificate, certificate));
        if (ldaps)
        {
            options = options.UseSsl();
        }

        LdapConnection connection = new(options) { ConnectionTimeout = TimeoutMilliseconds };
        try
        {
            await connection.ConnectAsync(url.Host, port, cancellationToken).ConfigureAwait(false);
            if (settings.StartTls && !ldaps)
            {
                await connection.StartTlsAsync(cancellationToken).ConfigureAwait(false);
            }

            return connection;
        }
        catch (Exception ex) when (ex is LdapException or SocketException or IOException or AuthenticationException)
        {
            connection.Dispose();
            LogUnreachable(settings.Url, ex);
            throw Unavailable(settings, ex);
        }
    }

    /// <summary>The server's certificate chains to the CA certificate given (a homelab's own authority).</summary>
    private static bool TrustedBy(string? caPem, X509Certificate? certificate)
    {
        if (caPem is null || certificate is null)
        {
            return false;
        }

        try
        {
            using X509Certificate2 authority = X509Certificate2.CreateFromPem(caPem);
            using X509Certificate2 server = new(certificate);
            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(authority);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(server);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>A value as an LDAP filter takes it (RFC 4515): <c>\ * ( )</c> and NUL escaped.</summary>
    internal static string Escape(string value)
    {
        StringBuilder escaped = new(value.Length);
        foreach (char c in value)
        {
            escaped.Append(c switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => c.ToString(),
            });
        }

        return escaped.ToString();
    }

    private static LdapUnavailableException Unavailable(LdapSettings settings, Exception ex) =>
        new(
            ex is LdapException ldap
                ? $"{settings.Url}: {(ldap.LdapErrorMessage is { Length: > 0 } message ? message : ldap.Message)}"
                : $"{settings.Url}: {ex.Message}",
            ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The LDAP directory {Url} can't be reached.")]
    private partial void LogUnreachable(string url, Exception error);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The directory refused the service account changing {Dn}'s password ({Why}): trying as the account.")]
    private partial void LogRefusedToService(string dn, string why);
}
