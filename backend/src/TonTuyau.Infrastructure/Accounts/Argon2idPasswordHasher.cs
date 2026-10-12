using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// Argon2id with OWASP's parameters (Password Storage Cheat Sheet: 19 MiB, 2 passes, 1 lane), a random salt per
/// password, stored as a PHC string: <c>$argon2id$v=19$m=19456,t=2,p=1$&lt;salt&gt;$&lt;hash&gt;</c> (unpadded
/// Base64). The string carries its parameters, so they can be raised later without breaking existing hashes.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    public const int MemoryKib = 19_456;
    public const int Iterations = 2;
    public const int Parallelism = 1;
    public const int SaltBytes = 16;
    public const int HashBytes = 32;

    private const string Prefix = "$argon2id$v=19$";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Derive(password, salt, MemoryKib, Iterations, Parallelism, HashBytes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}m={MemoryKib},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    /// <summary>True when <paramref name="password"/> is the one <paramref name="hash"/> (a PHC string) was made from.</summary>
    public bool Verify(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);
        // No password is empty (Argon2 refuses one): nothing to compute, whoever's account it would be.
        if (password.Length == 0
            || !TryParse(
                hash,
                out int memory,
                out int iterations,
                out int parallelism,
                out byte[] salt,
                out byte[] expected))
        {
            return false;
        }

        byte[] actual = Derive(password, salt, memory, iterations, parallelism, expected.Length);
        // Constant time: how long the comparison takes says nothing about how much of the hash matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int memory, int iterations, int parallelism, int length)
    {
        byte[] secret = Encoding.UTF8.GetBytes(password);
        try
        {
            using Argon2id argon = new(secret)
            {
                Salt = salt,
                MemorySize = memory,
                Iterations = iterations,
                DegreeOfParallelism = parallelism,
            };
            return argon.GetBytes(length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static bool TryParse(
        string phc,
        out int memory,
        out int iterations,
        out int parallelism,
        out byte[] salt,
        out byte[] hash)
    {
        memory = iterations = parallelism = 0;
        salt = hash = [];

        // "$argon2id$v=19$m=…,t=…,p=…$salt$hash" → "", "argon2id", "v=19", parameters, salt, hash.
        string[] parts = phc.Split('$');
        if (parts.Length != 6 || !phc.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parameters = parts[3].Split(',');
        if (parameters.Length != 3
            || !TryReadParameter(parameters[0], "m=", out memory)
            || !TryReadParameter(parameters[1], "t=", out iterations)
            || !TryReadParameter(parameters[2], "p=", out parallelism))
        {
            return false;
        }

        return TryDecode(parts[4], out salt)
            && TryDecode(parts[5], out hash)
            && salt.Length > 0
            && hash.Length > 0;
    }

    private static bool TryReadParameter(string text, string name, out int value)
    {
        value = 0;
        return text.StartsWith(name, StringComparison.Ordinal)
            && int.TryParse(text.AsSpan(name.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryDecode(string text, out byte[] bytes)
    {
        string padded = text.PadRight((text.Length + 3) / 4 * 4, '=');
        byte[] buffer = new byte[padded.Length / 4 * 3];
        bool decoded = Convert.TryFromBase64String(padded, buffer, out int written);
        bytes = decoded ? buffer[..written] : [];
        return decoded;
    }
}
