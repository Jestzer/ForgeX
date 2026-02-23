using System.Text;

namespace ForgeX.Core.IO;

public class EndianWriter : BinaryWriter
{
    public EndianType Endian { get; set; }

    public EndianWriter(Stream output, EndianType endianType)
        : base(output)
    {
        Endian = endianType;
    }

    public EndianWriter(Stream output, EndianType endianType, Encoding encoding)
        : base(output, encoding)
    {
        Endian = endianType;
    }

    private bool ShouldSwap() => Endian == EndianType.BigEndian;

    public override void Write(short value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt16(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(ushort value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToUInt16(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(int value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt32(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(uint value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToUInt32(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(long value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt64(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(float value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToSingle(bytes, 0);
        }
        base.Write(value);
    }

    public override void Write(double value)
    {
        if (ShouldSwap())
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToDouble(bytes, 0);
        }
        base.Write(value);
    }

    /// <summary>
    /// Writes a float with bytes always reversed (big-endian), regardless of endian setting.
    /// This matches the original Forge behavior for sandbox.map compatibility.
    /// </summary>
    public void WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToSingle(bytes, 0));
    }

    /// <summary>
    /// Writes an int32 with bytes always reversed (big-endian), regardless of endian setting.
    /// Used for tag ident values in sandbox.map for binary compatibility.
    /// </summary>
    public void WriteIdent(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToInt32(bytes, 0));
    }

    public override void Write(string value)
    {
        var chars = value.ToCharArray();
        if (ShouldSwap())
            Array.Reverse(chars);
        base.Write(chars);
    }

    public void Write(string value, int length)
    {
        var chars = value.ToCharArray();
        if (ShouldSwap())
            Array.Reverse(chars);
        base.Write(chars);
        for (int i = 0; i < length - value.Length; i++)
            Write((byte)0);
    }

    /// <summary>
    /// Writes a Unicode string with null byte before each character (big-endian UTF-16).
    /// </summary>
    public void WriteUnicode(string str, int length)
    {
        for (int i = 0; i < str.Length; i++)
        {
            Write((byte)0);
            Write(str[i]);
        }
        for (int i = 0; i < length - str.Length; i++)
            Write((byte)0);
    }

    public long Seek(int offset) => base.Seek(offset, SeekOrigin.Begin);
}
