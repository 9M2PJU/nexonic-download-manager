using System.Security.Cryptography;

namespace Nexonic.Core;

public enum ChecksumAlgorithm { Md5, Sha1, Sha256, Sha512 }

public static class Checksum
{
    public static string Name(ChecksumAlgorithm a) => a switch
    {
        ChecksumAlgorithm.Md5 => "MD5",
        ChecksumAlgorithm.Sha1 => "SHA-1",
        ChecksumAlgorithm.Sha256 => "SHA-256",
        _ => "SHA-512",
    };

    /// <summary>Reads "sha256:abc…", "SHA-256 abc…" or a bare hex value; the length of the hex value picks the algorithm.</summary>
    public static (ChecksumAlgorithm Algorithm, string Hex)? Parse(string? text)
    {
        var hex = (text ?? "").ToLowerInvariant().Split(new[] { ':', ' ', '=' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        if (hex.Length == 0 || !hex.All(Uri.IsHexDigit)) return null;
        return hex.Length switch
        {
            32 => (ChecksumAlgorithm.Md5, hex),
            40 => (ChecksumAlgorithm.Sha1, hex),
            64 => (ChecksumAlgorithm.Sha256, hex),
            128 => (ChecksumAlgorithm.Sha512, hex),
            _ => null,
        };
    }

    static HashAlgorithm Create(ChecksumAlgorithm a) => a switch
    {
        ChecksumAlgorithm.Md5 => MD5.Create(),
        ChecksumAlgorithm.Sha1 => SHA1.Create(),
        ChecksumAlgorithm.Sha256 => SHA256.Create(),
        _ => SHA512.Create(),
    };

    public static string Compute(ChecksumAlgorithm a, byte[] data)
    {
        using var h = Create(a);
        return Convert.ToHexString(h.ComputeHash(data)).ToLowerInvariant();
    }

    public static string Compute(ChecksumAlgorithm a, string path)
    {
        using var h = Create(a);
        using var f = File.OpenRead(path);
        return Convert.ToHexString(h.ComputeHash(f)).ToLowerInvariant();
    }

    /// <summary>"SHA-256 matches" or "SHA-256 does not match (got …)".</summary>
    public static string Verify(string expected, string path)
    {
        if (Parse(expected) is not { } p) return "Not a valid checksum";
        var actual = Compute(p.Algorithm, path);
        return actual == p.Hex ? $"{Name(p.Algorithm)} matches" : $"{Name(p.Algorithm)} does not match (got {actual})";
    }
}
