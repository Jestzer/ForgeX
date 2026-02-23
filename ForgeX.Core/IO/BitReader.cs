using System.Text;

namespace ForgeX.Core.IO;

/// <summary>
/// Reads individual bits from a byte array in big-endian (MSB-to-LSB) order.
/// Used for decoding MCC packed mvar bitstream format.
/// </summary>
public class BitReader
{
    private readonly byte[] _data;
    private int _bitOffset;

    public BitReader(byte[] data)
    {
        _data = data;
        _bitOffset = 0;
    }

    public int BitOffset => _bitOffset;
    public int BitsRemaining => (_data.Length * 8) - _bitOffset;

    /// <summary>
    /// Reads a single bit as a boolean.
    /// </summary>
    public bool ReadBool()
    {
        int byteIndex = _bitOffset / 8;
        int bitIndex = 7 - (_bitOffset % 8); // MSB first
        _bitOffset++;
        return ((_data[byteIndex] >> bitIndex) & 1) != 0;
    }

    /// <summary>
    /// Reads an unsigned integer from the specified number of bits.
    /// </summary>
    public uint ReadInteger(int bits)
    {
        if (bits == 0) return 0;
        if (bits > 32) throw new ArgumentOutOfRangeException(nameof(bits), "Cannot read more than 32 bits as uint.");

        uint value = 0;
        for (int i = 0; i < bits; i++)
        {
            value = (value << 1) | (ReadBool() ? 1u : 0u);
        }
        return value;
    }

    /// <summary>
    /// Reads a signed integer from the specified number of bits (two's complement).
    /// </summary>
    public int ReadSignedInteger(int bits)
    {
        if (bits == 0) return 0;
        uint raw = ReadInteger(bits);
        // Sign extend if the top bit is set
        if ((raw & (1u << (bits - 1))) != 0)
        {
            // Fill upper bits with 1s
            raw |= ~((1u << bits) - 1);
        }
        return unchecked((int)raw);
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer from the specified number of bits.
    /// </summary>
    public ulong ReadInteger64(int bits)
    {
        if (bits == 0) return 0;
        if (bits > 64) throw new ArgumentOutOfRangeException(nameof(bits), "Cannot read more than 64 bits.");

        ulong value = 0;
        for (int i = 0; i < bits; i++)
        {
            value = (value << 1) | (ReadBool() ? 1UL : 0UL);
        }
        return value;
    }

    /// <summary>
    /// Reads a quantized float: N bits dequantized from [min, max].
    /// Simple linear interpolation without exact midpoint support.
    /// </summary>
    public float ReadQuantizedFloat(int bits, float min, float max)
    {
        uint raw = ReadInteger(bits);
        uint maxVal = (1u << bits) - 1;
        if (maxVal == 0) return min;
        return min + (raw * (max - min) / maxVal);
    }

    /// <summary>
    /// Reads a quantized real value matching the Blam engine's dequantize_real.
    /// Supports exact_midpoint mode where step_count is adjusted for even distribution.
    /// </summary>
    public float ReadQuantizedReal(int bits, float min, float max, bool exactMidpoint)
    {
        int quantized = (int)ReadInteger(bits);
        return DequantizeReal(quantized, min, max, bits, exactMidpoint);
    }

    /// <summary>
    /// Dequantizes an integer value to a float within [min, max].
    /// Matches the Blam engine's dequantize_real function.
    /// </summary>
    public static float DequantizeReal(int quantized, float min, float max, int bits, bool exactMidpoint)
    {
        int stepCount = (1 << bits) - 1;
        if (exactMidpoint)
            stepCount -= stepCount % 2;

        if (quantized == 0) return min;
        if (quantized >= stepCount) return max;
        return ((stepCount - quantized) * min + quantized * max) / stepCount;
    }

    /// <summary>
    /// Reads a raw 32-bit IEEE 754 float (32 bits, no quantization).
    /// </summary>
    public float ReadRawFloat()
    {
        uint bits = ReadInteger(32);
        return BitConverter.Int32BitsToSingle(unchecked((int)bits));
    }

    /// <summary>
    /// Reads a UTF-16BE null-terminated string with a maximum character count.
    /// Variable-length: stops reading at null terminator without skipping remaining chars.
    /// Trims trailing control characters (artifacts from uninitialized Blam engine buffers).
    /// </summary>
    public string ReadStringWchar(int maxChars)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < maxChars; i++)
        {
            ushort c = (ushort)ReadInteger(16);
            if (c == 0)
                break;
            sb.Append((char)c);
        }
        return TrimControlChars(sb.ToString());
    }

    /// <summary>
    /// Reads a UTF-8 null-terminated string with a maximum byte count.
    /// Variable-length: stops reading at null terminator without skipping remaining bytes.
    /// Trims trailing control characters (artifacts from uninitialized Blam engine buffers).
    /// </summary>
    public string ReadStringUtf8(int maxBytes)
    {
        var bytes = new byte[maxBytes];
        int length = 0;
        for (int i = 0; i < maxBytes; i++)
        {
            byte b = (byte)ReadInteger(8);
            if (b == 0)
                break;
            bytes[length++] = b;
        }
        return TrimControlChars(Encoding.UTF8.GetString(bytes, 0, length));
    }

    /// <summary>
    /// Trims trailing non-printable control characters (0x01-0x1F except tab/newline/CR).
    /// Halo 3 string buffers sometimes contain uninitialized bytes before the null terminator.
    /// </summary>
    private static string TrimControlChars(string s)
    {
        int end = s.Length;
        while (end > 0 && s[end - 1] < ' ' && s[end - 1] != '\t' && s[end - 1] != '\n' && s[end - 1] != '\r')
            end--;
        return end == s.Length ? s : s[..end];
    }

    /// <summary>
    /// Reads raw bytes (byte-aligned in the bitstream).
    /// </summary>
    public byte[] ReadRawData(int byteCount)
    {
        var result = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            result[i] = (byte)ReadInteger(8);
        }
        return result;
    }

    /// <summary>
    /// Skips the specified number of bits.
    /// </summary>
    public void SkipBits(int bits)
    {
        _bitOffset += bits;
    }

    /// <summary>
    /// Seeks to an absolute bit position.
    /// </summary>
    public void SeekBits(int bitPosition)
    {
        _bitOffset = bitPosition;
    }
}
