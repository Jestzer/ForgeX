using System.Text;

namespace ForgeX.Core.IO;

/// <summary>
/// Writes individual bits to a byte buffer in big-endian (MSB-to-LSB) order.
/// Used for encoding MCC packed mvar bitstream format.
/// </summary>
public class BitWriter
{
    private byte[] _data;
    private int _bitOffset;

    public BitWriter(int initialCapacity = 8192)
    {
        _data = new byte[initialCapacity];
        _bitOffset = 0;
    }

    public int BitOffset => _bitOffset;

    private void EnsureCapacity(int bitsNeeded)
    {
        int bytesNeeded = (_bitOffset + bitsNeeded + 7) / 8;
        if (bytesNeeded > _data.Length)
        {
            int newSize = Math.Max(_data.Length * 2, bytesNeeded);
            Array.Resize(ref _data, newSize);
        }
    }

    /// <summary>
    /// Writes a single bit.
    /// </summary>
    public void WriteBool(bool value)
    {
        EnsureCapacity(1);
        int byteIndex = _bitOffset / 8;
        int bitIndex = 7 - (_bitOffset % 8); // MSB first
        if (value)
            _data[byteIndex] |= (byte)(1 << bitIndex);
        else
            _data[byteIndex] &= (byte)~(1 << bitIndex);
        _bitOffset++;
    }

    /// <summary>
    /// Writes an unsigned integer using the specified number of bits.
    /// </summary>
    public void WriteInteger(uint value, int bits)
    {
        if (bits == 0) return;
        if (bits > 32) throw new ArgumentOutOfRangeException(nameof(bits));

        EnsureCapacity(bits);
        for (int i = bits - 1; i >= 0; i--)
        {
            WriteBool(((value >> i) & 1) != 0);
        }
    }

    /// <summary>
    /// Writes a signed integer using the specified number of bits (two's complement).
    /// </summary>
    public void WriteSignedInteger(int value, int bits)
    {
        if (bits == 0) return;
        // For 32 bits, mask is 0xFFFFFFFF; (1u << 32) wraps in C# so handle it explicitly
        uint mask = bits >= 32 ? 0xFFFFFFFF : (1u << bits) - 1;
        WriteInteger(unchecked((uint)value) & mask, bits);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer using the specified number of bits.
    /// </summary>
    public void WriteInteger64(ulong value, int bits)
    {
        if (bits == 0) return;
        if (bits > 64) throw new ArgumentOutOfRangeException(nameof(bits));

        EnsureCapacity(bits);
        for (int i = bits - 1; i >= 0; i--)
        {
            WriteBool(((value >> i) & 1) != 0);
        }
    }

    /// <summary>
    /// Writes a quantized float: value quantized to N bits within [min, max].
    /// Formula: round((value - min) / (max - min) * ((1 &lt;&lt; bits) - 1))
    /// </summary>
    public void WriteQuantizedFloat(float value, int bits, float min, float max)
    {
        uint maxVal = (1u << bits) - 1;
        float normalized = (value - min) / (max - min);
        normalized = Math.Clamp(normalized, 0f, 1f);
        uint quantized = (uint)Math.Round(normalized * maxVal);
        WriteInteger(quantized, bits);
    }

    /// <summary>
    /// Writes a quantized real value matching the Blam engine's quantize_real.
    /// Inverse of BitReader.ReadQuantizedReal. Supports exact_midpoint mode.
    /// </summary>
    public void WriteQuantizedReal(float value, int bits, float min, float max, bool exactMidpoint)
    {
        int stepCount = (1 << bits) - 1;
        if (exactMidpoint)
            stepCount -= stepCount % 2;

        float normalized = (value - min) / (max - min);
        int quantized = (int)Math.Clamp(Math.Round(normalized * stepCount), 0, stepCount);
        WriteInteger((uint)quantized, bits);
    }

    /// <summary>
    /// Writes a raw 32-bit IEEE 754 float (32 bits, no quantization).
    /// </summary>
    public void WriteRawFloat(float value)
    {
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        WriteInteger(bits, 32);
    }

    /// <summary>
    /// Writes a UTF-16BE null-terminated string (variable-length, no padding).
    /// </summary>
    public void WriteStringWchar(string value, int maxChars)
    {
        int charsToWrite = Math.Min(value.Length, maxChars - 1);
        for (int i = 0; i < charsToWrite; i++)
        {
            WriteInteger((ushort)value[i], 16);
        }
        WriteInteger(0, 16); // null terminator
    }

    /// <summary>
    /// Writes a UTF-8 null-terminated string (variable-length, no padding).
    /// </summary>
    public void WriteStringUtf8(string value, int maxBytes)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        int bytesToWrite = Math.Min(utf8.Length, maxBytes - 1);
        for (int i = 0; i < bytesToWrite; i++)
        {
            WriteInteger(utf8[i], 8);
        }
        WriteInteger(0, 8); // null terminator
    }

    /// <summary>
    /// Writes raw bytes.
    /// </summary>
    public void WriteRawData(byte[] data)
    {
        foreach (byte b in data)
        {
            WriteInteger(b, 8);
        }
    }

    /// <summary>
    /// Returns the written data as a byte array (trimmed to actual length).
    /// </summary>
    public byte[] ToArray()
    {
        int byteLength = (_bitOffset + 7) / 8;
        var result = new byte[byteLength];
        Array.Copy(_data, result, byteLength);
        return result;
    }
}
