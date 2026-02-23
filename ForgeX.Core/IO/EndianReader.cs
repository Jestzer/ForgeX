using System.Text;

namespace ForgeX.Core.IO;

public class EndianReader : BinaryReader
{
    public EndianType Endian { get; set; }

    public EndianReader(Stream input, EndianType endianType)
        : base(input)
    {
        Endian = endianType;
    }

    public EndianReader(Stream input, EndianType endianType, Encoding encoding)
        : base(input, encoding)
    {
        Endian = endianType;
    }

    public override short ReadInt16() => ReadInt16(Endian);

    public short ReadInt16(EndianType endian)
    {
        var value = base.ReadInt16();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt16(bytes, 0);
        }
        return value;
    }

    public override ushort ReadUInt16() => ReadUInt16(Endian);

    public ushort ReadUInt16(EndianType endian)
    {
        var value = base.ReadUInt16();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToUInt16(bytes, 0);
        }
        return value;
    }

    public override int ReadInt32() => ReadInt32(Endian);

    public int ReadInt32(EndianType endian)
    {
        var value = base.ReadInt32();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt32(bytes, 0);
        }
        return value;
    }

    public override uint ReadUInt32() => ReadUInt32(Endian);

    public uint ReadUInt32(EndianType endian)
    {
        var value = base.ReadUInt32();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToUInt32(bytes, 0);
        }
        return value;
    }

    public override long ReadInt64() => ReadInt64(Endian);

    public long ReadInt64(EndianType endian)
    {
        var value = base.ReadInt64();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt64(bytes, 0);
        }
        return value;
    }

    public override float ReadSingle() => ReadSingle(Endian);

    public float ReadSingle(EndianType endian)
    {
        var value = base.ReadSingle();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToSingle(bytes, 0);
        }
        return value;
    }

    public override double ReadDouble() => ReadDouble(Endian);

    public double ReadDouble(EndianType endian)
    {
        var value = base.ReadDouble();
        if (endian == EndianType.BigEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToDouble(bytes, 0);
        }
        return value;
    }

    public override byte[] ReadBytes(int count) => ReadBytes(count, Endian);

    public byte[] ReadBytes(int count, EndianType endian)
    {
        var bytes = base.ReadBytes(count);
        if (endian == EndianType.BigEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    /// <summary>
    /// Reads bytes without endian swapping (raw read).
    /// </summary>
    public byte[] XReadBytes(int count)
    {
        if (BaseStream == null)
            throw new InvalidOperationException("Stream is not open.");

        var buffer = new byte[count];
        int offset = 0;
        while (count > 0)
        {
            int read = BaseStream.Read(buffer, offset, count);
            if (read == 0) break;
            offset += read;
            count -= read;
        }
        if (offset != buffer.Length)
        {
            var trimmed = new byte[offset];
            Buffer.BlockCopy(buffer, 0, trimmed, 0, offset);
            return trimmed;
        }
        return buffer;
    }

    public override char[] ReadChars(int count) => ReadChars(count, Endian);

    public char[] ReadChars(int count, EndianType endian)
    {
        var chars = Encoding.ASCII.GetChars(XReadBytes(count));
        if (endian == EndianType.BigEndian)
            Array.Reverse(chars);
        return chars;
    }

    public override char ReadChar()
    {
        return Encoding.ASCII.GetChars(base.ReadBytes(1))[0];
    }

    public string ReadString(uint length)
    {
        var sb = new StringBuilder();
        for (uint i = 0; i < length; i++)
        {
            char c = ReadChar();
            if (c != '\0')
                sb.Append(c);
        }
        return sb.ToString();
    }

    public int ReadInt24() => ReadInt24(Endian);

    public int ReadInt24(EndianType endian)
    {
        var buffer = new byte[4];
        if (endian == EndianType.BigEndian)
            Read(buffer, 1, 3);
        else
            Read(buffer, 0, 3);

        if (endian == EndianType.BigEndian)
            Array.Reverse(buffer);

        return BitConverter.ToInt32(buffer, 0);
    }

    public string XReadUnicodeString(int length) => XReadUnicodeString(length, Endian);

    public string XReadUnicodeString(int length, EndianType endian)
    {
        var sb = new StringBuilder();
        int charsRead = 0;
        for (int i = 0; i < length; i++)
        {
            char c = (char)ReadUInt16(endian);
            charsRead++;
            if (c == '\0') break;
            sb.Append(c);
        }
        // Skip remaining characters
        int remaining = (length - charsRead) * 2;
        if (remaining > 0)
            BaseStream.Seek(remaining, SeekOrigin.Current);
        return sb.ToString();
    }

    public long Seek(int offset) => BaseStream.Seek(offset, SeekOrigin.Begin);
}
