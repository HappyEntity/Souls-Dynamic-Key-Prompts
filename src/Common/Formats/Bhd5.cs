using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace DynamicKeyPrompts.Formats;

/// FromSoftware BHD5/BDT archives (Dark Souls II, Dark Souls III). The .bhd header is "encrypted" with
/// the archive's RSA public key (raw RSA per block); the .bdt holds the files, optionally with
/// AES-128-ECB encrypted ranges.
public sealed class Bhd5
{
    readonly byte[] _bhd;
    readonly string _bdt;
    readonly Dictionary<uint, int> _entries = new();
    readonly bool _ds3;

    /// <param name="ds3">Dark Souls III entry layout (0x28 bytes, with the unpadded size).</param>
    public Bhd5(string bhdPath, string publicKeyPem, string bdtPath, bool ds3)
    {
        _bdt = bdtPath;
        _ds3 = ds3;
        byte[] raw = File.ReadAllBytes(bhdPath);
        // Tools like BootBoost replace the headers with decrypted copies (the game reads those as they are).
        _bhd = raw.AsSpan(0, 4).SequenceEqual("BHD5"u8) ? raw : RsaDecrypt(raw, publicKeyPem);
        if (Encoding.ASCII.GetString(_bhd, 0, 4) != "BHD5") throw new InvalidDataException("BHD5 header not decrypted");
        int bucketCount = BitConverter.ToInt32(_bhd, 0x10), buckets = BitConverter.ToInt32(_bhd, 0x14);
        int entrySize = ds3 ? 0x28 : 0x20;
        for (int i = 0; i < bucketCount; i++)
        {
            int n = BitConverter.ToInt32(_bhd, buckets + i * 8), off = BitConverter.ToInt32(_bhd, buckets + i * 8 + 4);
            for (int j = 0; j < n; j++) _entries.TryAdd(BitConverter.ToUInt32(_bhd, off + j * entrySize), off + j * entrySize);
        }
    }

    /// Number of files in the archive.
    public int Count => _entries.Count;

    /// Path hash: lower case, forward slashes, leading slash; h = h * 37 + c.
    public static uint Hash(string path)
    {
        path = path.ToLowerInvariant().Replace('\\', '/');
        if (!path.StartsWith('/')) path = "/" + path;
        uint h = 0;
        foreach (char c in path) h = h * 37 + c;
        return h;
    }

    /// The file's bytes as stored (usually DCX), or null if the archive does not have it.
    public byte[]? Read(string path)
    {
        if (!_entries.TryGetValue(Hash(path), out int e)) return null;
        int size = BitConverter.ToInt32(_bhd, e + 4);
        long offset = BitConverter.ToInt64(_bhd, e + 8), aes = BitConverter.ToInt64(_bhd, e + 0x18);
        long unpadded = _ds3 ? BitConverter.ToInt64(_bhd, e + 0x20) : 0;

        var data = new byte[size];
        using (var fs = File.OpenRead(_bdt))
        {
            fs.Position = offset;
            fs.ReadExactly(data);
        }
        if (aes != 0)
        {
            using var cipher = Aes.Create();
            cipher.Key = _bhd.AsSpan((int)aes, 16).ToArray();
            int ranges = BitConverter.ToInt32(_bhd, (int)aes + 16);
            for (int r = 0; r < ranges; r++)
            {
                long start = BitConverter.ToInt64(_bhd, (int)aes + 20 + r * 16), end = BitConverter.ToInt64(_bhd, (int)aes + 28 + r * 16);
                if (start == -1 || end <= start) continue;
                cipher.DecryptEcb(data.AsSpan((int)start, (int)(end - start)), PaddingMode.None).CopyTo(data, (int)start);
            }
        }
        return unpadded > 0 && unpadded < data.Length ? data[..(int)unpadded] : data;
    }

    static byte[] RsaDecrypt(byte[] input, string pem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var p = rsa.ExportParameters(false);
        var n = new BigInteger(p.Modulus!, true, true);
        var e = new BigInteger(p.Exponent!, true, true);
        int keySize = p.Modulus!.Length, outSize = keySize - 1;
        var o = new MemoryStream();
        for (int i = 0; i + keySize <= input.Length; i += keySize)
        {
            var m = BigInteger.ModPow(new BigInteger(input.AsSpan(i, keySize), true, true), e, n).ToByteArray(true, true);
            var block = new byte[outSize];
            m.CopyTo(block, outSize - m.Length);
            o.Write(block);
        }
        return o.ToArray();
    }
}
