using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Sentium.Identity.Features.Devices;

/// <summary>
/// Generates and hashes the two Device secrets: the short pairing code a person types, and the long-lived token the
/// app stores. Both are random values, so a plain SHA-256 is enough to store them; the short code additionally
/// relies on its 10-minute life and the rate limit on the pairing endpoint.
/// </summary>
internal static class DeviceSecrets
{
    /// <summary>
    /// No 0/O, 1/I/L: a code is read off one screen and typed on another.
    /// </summary>
    private const string PairingAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private const int TokenBytes = 32;

    public static string NewPairingCode() => RandomNumberGenerator.GetString(PairingAlphabet, DeviceLimits.PairingCodeLength);

    /// <summary>
    /// Upper-cases the code and drops everything that is not a letter or digit, so "abcd-efgh" and "ABCD EFGH" match.
    /// </summary>
    public static string NormalizePairingCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return new string([.. code.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant)]);
    }

    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public static string Hash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
