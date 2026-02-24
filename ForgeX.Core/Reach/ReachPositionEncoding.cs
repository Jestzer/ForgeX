using ForgeX.Core.IO;

namespace ForgeX.Core.Reach;

/// <summary>
/// Implements Reach's adaptive position encoding.
/// Each axis gets a variable number of bits based on the world bounds range.
/// Ported from the engine's _sub4DC8E0 function.
/// </summary>
public static class ReachPositionEncoding
{
    private const float MinimumUnit16Bit = 0.00833333333f; // ~1/120

    /// <summary>
    /// Computes per-axis bit counts from world bounds using the Reach engine's algorithm.
    /// </summary>
    public static (int BitsX, int BitsY, int BitsZ) ComputeAxisBitCounts(
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
        int baseBits = 21)
    {
        float[] ranges = { xMax - xMin, yMax - yMin, zMax - zMin };
        int[] result = { baseBits, baseBits, baseBits };

        float minStep;
        if (baseBits > 16)
            minStep = MinimumUnit16Bit / (1 << (baseBits - 16));
        else
            minStep = (1 << (16 - baseBits)) * MinimumUnit16Bit;

        if (minStep < 0.0001f)
        {
            // Fallback: maximum 26 bits per axis
            return (26, 26, 26);
        }

        minStep *= 2;

        for (int i = 0; i < 3; i++)
        {
            int edx = Math.Min(0x800000, (int)Math.Floor(Math.Ceiling(ranges[i] / minStep)));
            int ecx = -1;
            if (edx > 0)
                ecx = HighestBitSet(edx);

            int r8 = 0;
            if (ecx != -1)
            {
                int eax = (1 << ecx) - 1;
                r8 = ecx + (((edx & eax) != 0) ? 1 : 0);
            }
            result[i] = Math.Min(26, r8);
        }

        return (result[0], result[1], result[2]);
    }

    /// <summary>
    /// Decodes a position value from the bitstream using the adaptive encoding.
    /// Formula: worldPos = min + (raw + 0.5) * (range / 2^bits)
    /// </summary>
    public static float DecodePosition(BitReader bits, int bitCount, float min, float max)
    {
        if (bitCount == 0) return min;

        uint raw = bits.ReadInteger(bitCount);
        float range = max - min;
        float divisor = 1u << bitCount;
        return min + (raw + 0.5f) * (range / divisor);
    }

    /// <summary>
    /// Encodes a world position value for the bitstream using the adaptive encoding.
    /// Inverse of DecodePosition.
    /// Formula: raw = floor((worldPos - min) * 2^bits / range - 0.5)
    /// </summary>
    public static uint EncodePosition(float worldPos, int bitCount, float min, float max)
    {
        if (bitCount == 0) return 0;

        float range = max - min;
        uint maxVal = (1u << bitCount) - 1;
        float raw = (worldPos - min) / range * (1u << bitCount) - 0.5f;
        return (uint)Math.Clamp((int)MathF.Floor(raw), 0, (int)maxVal);
    }

    private static int HighestBitSet(int value)
    {
        int r = 0;
        int v = value;
        while ((v >>= 1) != 0)
            r++;
        return r;
    }
}
