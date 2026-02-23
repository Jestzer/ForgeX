using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ForgeX.Core.IO;

namespace ForgeX.Core.Xbox360;

/// <summary>
/// Reads and writes Xbox 360 STFS/CON container files.
/// Ported from X360.dll XContainer class with WinForms dependencies removed.
/// </summary>
public class StfsContainer
{
    private const uint MAGIC_CON_MASK = 0xFFFFFF00; // Check first 3 bytes: "CON"
    private const uint MAGIC_CON_PREFIX = 0x434F4E00; // "CON" in first 3 bytes

    public int BlockShift { get; set; }
    public int[] BlockStep { get; set; } = new int[2];
    public StfsVolumeDescriptor STFS { get; set; } = null!;
    public bool Resigned { get; set; }
    public bool Rehashed { get; set; }
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public byte[] SignedSHA1 { get; set; } = Array.Empty<byte>();
    public ContentType ContentType { get; set; }
    public uint TitleID { get; set; }
    public byte[] ProfileID { get; set; } = Array.Empty<byte>();
    public string DeviceID { get; set; } = string.Empty;
    public byte[] ConsoleID { get; set; } = Array.Empty<byte>();
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TitleName { get; set; } = string.Empty;
    public string HashTableSHA1 { get; set; } = string.Empty;
    public int HashingBlock { get; set; }
    public int HashCount { get; set; }
    public MasterHashTable? HashTable { get; set; }
    public byte[] ThumbnailData { get; set; } = Array.Empty<byte>();
    public EndianIO IO { get; set; } = null!;
    public List<ContainerFileEntry> Entries { get; set; } = new();
    public bool IsHeaderHashed { get; set; }

    public StfsContainer(string fileName)
    {
        IO = new EndianIO(fileName, EndianType.BigEndian, keepOpen: true);
        Read();
        RefreshHashTableInfo();

        try
        {
            VerifyRSA();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // SHA1 RSA verification may be disabled by OS security policy
            // (e.g., OpenSSL 3.x on modern Linux). Non-fatal - just mark as unverified.
            Resigned = false;
        }
    }

    public void Close()
    {
        IO.Close();
    }

    public void RefreshHashTableInfo()
    {
        int offset = HashingBlock == 603979777 ? 745472 : 749568;
        IsHeaderHashed = true;
        Rehashed = true;
        HashTable = new MasterHashTable(offset, this);
    }

    public ContainerFileEntry? GetEntryByFileName(string name)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (name == Entries[i].FileName)
                return Entries[i];
        }
        return null;
    }

    public int GetBlockOffset(int cluster)
    {
        int backingBlock = STFSComputeBackingDataBlockNumber(cluster);
        return 40960 + backingBlock * 4096;
    }

    public int STFSComputeBackingDataBlockNumber(int cluster)
    {
        int num = (cluster + 170) / 170;
        num <<= BlockShift;
        int result = num + cluster;

        if (cluster < 170)
            return result;

        num = (cluster + 28900) / 28900;
        num <<= BlockShift;
        result += num;

        if (cluster < 28900)
            return result;

        num <<= 1;
        return result + num;
    }

    public void Read()
    {
        IO.Reader.Seek(4);
        PublicKey = IO.Reader.ReadBytes(424, EndianType.LittleEndian);
        SignedSHA1 = IO.Reader.ReadBytes(128);

        IO.Reader.Seek(836);
        ContentType = (ContentType)IO.Reader.ReadInt32();

        IO.Reader.Seek(864);
        TitleID = IO.Reader.ReadUInt32();

        IO.Reader.Seek(876);
        ConsoleID = IO.Reader.ReadBytes(20);

        IO.Reader.BaseStream.Position = 889;
        byte[] stfsData = IO.Reader.ReadBytes(36, EndianType.LittleEndian);
        STFS = new StfsVolumeDescriptor(stfsData);

        IO.Reader.Seek(881);
        ProfileID = IO.Reader.ReadBytes(8);

        IO.Reader.Seek(889);
        HashingBlock = IO.Reader.ReadInt32();

        IO.Reader.Seek(897);
        HashTableSHA1 = StfsSigning.BytesToHexString(IO.Reader.ReadBytes(20, EndianType.LittleEndian));

        IO.Reader.Seek(917);
        HashCount = IO.Reader.ReadInt32();

        IO.Reader.Seek(1021);
        DeviceID = StfsSigning.BytesToHexString(IO.Reader.ReadBytes(20));

        IO.Reader.Seek(1041);
        DisplayName = IO.Reader.XReadUnicodeString(128);

        IO.Reader.Seek(3345);
        Description = IO.Reader.XReadUnicodeString(128);

        IO.Reader.Seek(5777);
        TitleName = IO.Reader.XReadUnicodeString(64);

        IO.Reader.Seek(5906);
        int thumbnailSize = IO.Reader.ReadInt32();

        IO.Reader.Seek(5914);
        ThumbnailData = IO.Reader.XReadBytes(thumbnailSize);

        SetupSTFS();

        // Validate magic number - first 3 bytes must be "CON"
        IO.Reader.BaseStream.Position = 0;
        uint magic = IO.Reader.ReadUInt32(EndianType.BigEndian);
        if ((magic & MAGIC_CON_MASK) != MAGIC_CON_PREFIX)
            throw new InvalidDataException($"Not a valid CON file. Magic: 0x{magic:X8}");

        // Read directory entries
        Entries = new List<ContainerFileEntry>();
        IO.Reader.Seek(GetBlockOffset(STFS.DirectoryRelocator));

        while (true)
        {
            var entry = new ContainerFileEntry(this);
            entry.Read();
            if (!entry.IsValid)
                break;
            Entries.Add(entry);
        }
    }

    public void SetupSTFS()
    {
        BlockStep = new int[2];
        if ((STFS.BlockSeparation & 1) == 1)
        {
            BlockShift = 0;
            BlockStep[0] = 171;
            BlockStep[1] = 29071;
        }
        else
        {
            BlockShift = 1;
            BlockStep[0] = 172;
            BlockStep[1] = 29242;
        }
    }

    public void Resign()
    {
        HashTable!.ReHash();

        byte[] kvData = KeyVaultLoader.Load();
        var signing = new StfsSigning(new BinaryReader(new MemoryStream(kvData)));

        // Hash the content metadata
        IO.Reader.BaseStream.Position = 836;
        byte[] metadataBytes = IO.Reader.ReadBytes(40124, EndianType.LittleEndian);
        byte[] metadataHash = StfsSigning.CalculateHash(metadataBytes, Xbox360.HashAlgorithm.SHA1);
        IO.Writer.BaseStream.Position = 812;
        IO.Writer.Write(metadataHash);

        // Hash and sign the header
        IO.Reader.BaseStream.Position = 556;
        byte[] headerBytes = IO.Reader.ReadBytes(280, EndianType.LittleEndian);
        byte[] headerHash = StfsSigning.CalculateHash(headerBytes, Xbox360.HashAlgorithm.SHA1);
        byte[] signature = signing.SignHash(headerHash);
        Array.Reverse(signature);

        IO.Writer.BaseStream.Position = 428;
        IO.Writer.Write(signature);
        // Preserve the original magic bytes (first 4 bytes of the file)
        IO.Reader.BaseStream.Position = 0;
        uint originalMagic = IO.Reader.ReadUInt32(EndianType.BigEndian);
        IO.Writer.BaseStream.Position = 0;
        IO.Writer.Write(originalMagic);
        IO.Writer.Write(signing.ReturnPublicKey());

        signing.KeyVaultReader.Close();
        Resigned = true;
    }

    public void VerifyRSA()
    {
        using var pubKeyStream = new MemoryStream(PublicKey);
        using var pubKeyReader = new BinaryReader(pubKeyStream);

        pubKeyReader.BaseStream.Position = 36;
        byte[] exponent = pubKeyReader.ReadBytes(4);
        byte[] modulus = pubKeyReader.ReadBytes(128);
        ReverseInBlocks(modulus, 8);

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Exponent = exponent,
            Modulus = modulus
        });

        IO.Reader.BaseStream.Position = 556;
        byte[] headerBytes = IO.Reader.ReadBytes(280, EndianType.LittleEndian);
        byte[] headerHash = StfsSigning.CalculateHash(headerBytes, Xbox360.HashAlgorithm.SHA1);

        Resigned = rsa.VerifyHash(headerHash, SignedSHA1, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
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
}
