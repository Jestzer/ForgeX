using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ForgeX.Core.Xbox360;

public enum HashAlgorithm
{
    MD5,
    SHA1,
    SHA256,
    SHA384,
    SHA512
}

/// <summary>
/// Handles SHA1 hashing and RSA-2048 signing for Xbox 360 STFS containers.
/// </summary>
public class StfsSigning
{
    public RSA Rsa { get; set; }
    public BinaryReader KeyVaultReader { get; set; }

    public StfsSigning(BinaryReader kvReader)
    {
        KeyVaultReader = kvReader;
        kvReader.BaseStream.Position = 652;
        byte[] exponent = kvReader.ReadBytes(4);
        kvReader.BaseStream.Position += 8;
        byte[] modulus = kvReader.ReadBytes(128);
        byte[] p = kvReader.ReadBytes(64);
        byte[] q = kvReader.ReadBytes(64);
        byte[] dp = kvReader.ReadBytes(64);
        byte[] dq = kvReader.ReadBytes(64);
        byte[] inverseQ = kvReader.ReadBytes(64);
        byte[] d = new byte[]
        {
            109, 76, 207, 61, 232, 101, 81, 255, 45, 172,
            193, 144, 231, 71, 235, 198, 116, 88, 208, 45,
            25, 8, 172, 121, 206, 208, 29, 163, 28, 195,
            46, 57, 142, 199, 239, 102, 250, 228, 47, 16,
            66, 168, 78, 231, 161, 253, 244, 240, 203, 100,
            103, 166, 16, 77, 109, 58, 86, 157, 31, 236,
            81, 252, 194, 38, 69, 194, 222, 249, 155, 76,
            76, 147, 77, 168, 43, 72, 172, 237, 215, 252,
            234, 233, 114, 251, 178, 57, 136, 193, 7, 52,
            111, 42, 7, 126, 151, 129, 245, 2, 33, 250,
            205, 221, 48, 221, 229, 65, 179, 74, 34, 115,
            128, 137, 43, 158, 144, 175, 196, 10, 138, 80,
            21, 15, 189, 110, 212, 149, 55, 121
        };

        ReverseInBlocks(q, 8);
        ReverseInBlocks(p, 8);
        ReverseInBlocks(modulus, 8);
        ReverseInBlocks(inverseQ, 8);
        ReverseInBlocks(dp, 8);
        ReverseInBlocks(dq, 8);

        var parameters = new RSAParameters
        {
            D = d,
            Modulus = modulus,
            InverseQ = inverseQ,
            DQ = dq,
            DP = dp,
            Q = q,
            P = p,
            Exponent = exponent
        };

        Rsa = RSA.Create();
        Rsa.ImportParameters(parameters);
    }

    private static void ReverseInBlocks(byte[] key, int blockSize)
    {
        int half = key.Length / 2;
        for (int i = 1; i <= half; i++)
        {
            int idx1 = i - 1;
            int idx2 = key.Length - idx1 / blockSize * blockSize - blockSize + idx1 % blockSize;
            (key[idx1], key[idx2]) = (key[idx2], key[idx1]);
        }
    }

    public byte[] ReturnPublicKey()
    {
        KeyVaultReader.BaseStream.Position = 2488;
        return KeyVaultReader.ReadBytes(424);
    }

    public byte[] SignHash(byte[] sha1Hash)
    {
        return Rsa.SignHash(sha1Hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    }

    public static byte[] CalculateHash(byte[] source, HashAlgorithm algorithm)
    {
        return algorithm switch
        {
            HashAlgorithm.MD5 => System.Security.Cryptography.MD5.HashData(source),
            HashAlgorithm.SHA1 => System.Security.Cryptography.SHA1.HashData(source),
            HashAlgorithm.SHA256 => System.Security.Cryptography.SHA256.HashData(source),
            HashAlgorithm.SHA384 => System.Security.Cryptography.SHA384.HashData(source),
            HashAlgorithm.SHA512 => System.Security.Cryptography.SHA512.HashData(source),
            _ => System.Security.Cryptography.SHA1.HashData(source),
        };
    }

    public static string BytesToHexString(byte[] buffer)
    {
        return Convert.ToHexString(buffer);
    }
}
