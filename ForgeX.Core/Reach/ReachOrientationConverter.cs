using ForgeX.Core.IO;
using ForgeX.Core.Halo3;

namespace ForgeX.Core.Reach;

/// <summary>
/// Encodes and decodes Reach's orientation encoding: 20-bit axis vector (lookup-table cube-face)
/// + 14-bit rotation angle. Different from H3's 19-bit cube-face + 8-bit forward angle.
/// </summary>
public static class ReachOrientationConverter
{
    // Lookup table entry [14] for 20-bit encoding (bitcount - 6 = 14)
    private const int AxisDivisor = 174762;    // 0x2AAAA
    private const int AxisSubdivisions = 417;  // 0x1A1

    /// <summary>
    /// Reads orientation from the Reach bitstream and returns forward/up vectors.
    /// Reach uses axis-angle encoding: a unit axis vector + rotation angle around it.
    /// </summary>
    public static (float FwdI, float FwdJ, float FwdK, float UpI, float UpJ, float UpK)
        ReadOrientation(BitReader bits)
    {
        // 1-bit: is axis the default (0,0,1)?
        bool axisIsDefault = bits.ReadBool();

        float axisI, axisJ, axisK;
        if (axisIsDefault)
        {
            axisI = 0f;
            axisJ = 0f;
            axisK = 1f;
        }
        else
        {
            // 20-bit encoded axis vector
            uint raw = bits.ReadInteger(20);
            (axisI, axisJ, axisK) = Decode20BitAxis(raw);
        }

        // 14-bit rotation angle
        // Decoded as compressed float: (raw + 0.5) * (2*PI / 2^14) - PI
        uint angleRaw = bits.ReadInteger(14);
        float angle = DecodeAngle14(angleRaw);

        // The axis is the up vector in Reach's axis-angle encoding
        // The angle defines rotation of the forward vector around this up axis
        // Use the existing Blam engine math to compute forward from up + angle
        var (fwdI, fwdJ, fwdK) = OrientationConverter.AngleToAxesInternal(
            axisI, axisJ, axisK, angle);

        return (fwdI, fwdJ, fwdK, axisI, axisJ, axisK);
    }

    /// <summary>
    /// Decodes a 20-bit raw value to a unit vector using cube-face projection.
    /// Uses lookup table entry [14]: divisor=174762, subdivisions=417.
    /// </summary>
    public static (float I, float J, float K) Decode20BitAxis(uint raw)
    {
        // Decompose: face = raw / divisor, remainder for u/v
        int face = (int)(raw / AxisDivisor);
        int remainder = (int)(raw % AxisDivisor);

        int uQuantized = remainder / AxisSubdivisions;
        int vQuantized = remainder % AxisSubdivisions;

        // Dequantize u and v to [-1, 1] with midpoint snapping
        float u = DequantizeAxis(uQuantized, AxisSubdivisions - 1);
        float v = DequantizeAxis(vQuantized, AxisSubdivisions - 1);

        // Project onto cube face
        float i, j, k;
        switch (face)
        {
            case 0: i = 1f;  j = u;  k = v;  break; // +X
            case 1: i = u;   j = 1f; k = v;  break; // +Y
            case 2: i = u;   j = v;  k = 1f; break;  // +Z
            case 3: i = -1f; j = u;  k = v;  break; // -X
            case 4: i = u;   j = -1f; k = v; break;  // -Y
            case 5: i = u;   j = v;  k = -1f; break; // -Z
            default: i = 0f; j = 0f; k = 1f; break;
        }

        // Normalize to unit vector
        float len = MathF.Sqrt(i * i + j * j + k * k);
        if (len > 1e-6f)
        {
            i /= len;
            j /= len;
            k /= len;
        }

        return (i, j, k);
    }

    /// <summary>
    /// Dequantizes a value from [0, stepCount] to [-1, 1] with exact midpoint snapping.
    /// Formula: value = quantized * (2.0 / stepCount) - 1.0 + (1.0 / stepCount)
    /// If quantized * 2 == stepCount - 1, snap to exactly 0.
    /// </summary>
    private static float DequantizeAxis(int quantized, int stepCount)
    {
        // Check for exact midpoint
        if (quantized * 2 == stepCount - 1)
            return 0f;

        float scale = 2.0f / stepCount;
        return quantized * scale - 1.0f + scale * 0.5f;
    }

    /// <summary>
    /// Decodes a 14-bit angle to [-PI, PI] using the Reach compressed float formula.
    /// Formula: angle = raw * (2*PI / 16384) - PI + (PI / 16384)
    /// Equivalent to: (raw + 0.5) * (2*PI / 2^14) - PI
    /// </summary>
    private static float DecodeAngle14(uint raw)
    {
        const float twoPi = 2f * MathF.PI;
        const float range = twoPi;
        const float divisor = 16384f; // 2^14
        return -MathF.PI + (raw + 0.5f) * (range / divisor);
    }

    /// <summary>
    /// Writes orientation to the Reach bitstream. Inverse of ReadOrientation.
    /// Encodes up vector (default or 20-bit) + 14-bit forward angle.
    /// </summary>
    public static void WriteOrientation(BitWriter bits,
        float fwdI, float fwdJ, float fwdK,
        float upI, float upJ, float upK)
    {
        bool isDefault = MathF.Abs(upI) < 1e-4f && MathF.Abs(upJ) < 1e-4f && upK > 0.999f;
        bits.WriteBool(isDefault);
        if (!isDefault)
        {
            uint encoded = Encode20BitAxis(upI, upJ, upK);
            bits.WriteInteger(encoded, 20);
        }

        float angle = OrientationConverter.AxesToAngle(upI, upJ, upK, fwdI, fwdJ, fwdK);
        bits.WriteInteger(EncodeAngle14(angle), 14);
    }

    /// <summary>
    /// Encodes a unit vector to 20-bit cube-face projection. Inverse of Decode20BitAxis.
    /// </summary>
    public static uint Encode20BitAxis(float i, float j, float k)
    {
        // Normalize
        float len = MathF.Sqrt(i * i + j * j + k * k);
        if (len > 1e-6f) { i /= len; j /= len; k /= len; }

        float absI = MathF.Abs(i), absJ = MathF.Abs(j), absK = MathF.Abs(k);
        int face;
        float u, v;

        if (absI >= absJ && absI >= absK)
        {
            if (i > 0) { face = 0; u = j / absI; v = k / absI; }
            else        { face = 3; u = j / absI; v = k / absI; }
        }
        else if (absJ >= absI && absJ >= absK)
        {
            if (j > 0) { face = 1; u = i / absJ; v = k / absJ; }
            else        { face = 4; u = i / absJ; v = k / absJ; }
        }
        else
        {
            if (k > 0) { face = 2; u = i / absK; v = j / absK; }
            else        { face = 5; u = i / absK; v = j / absK; }
        }

        int qu = QuantizeAxis(u, AxisSubdivisions - 1);
        int qv = QuantizeAxis(v, AxisSubdivisions - 1);
        return (uint)(face * AxisDivisor + qu * AxisSubdivisions + qv);
    }

    /// <summary>
    /// Quantizes a value from [-1, 1] to [0, stepCount]. Inverse of DequantizeAxis.
    /// </summary>
    private static int QuantizeAxis(float value, int stepCount)
    {
        float scale = 2.0f / stepCount;
        int quantized = (int)MathF.Round((value + 1.0f - scale * 0.5f) / scale);
        return Math.Clamp(quantized, 0, stepCount);
    }

    /// <summary>
    /// Encodes an angle from [-PI, PI] to 14-bit. Inverse of DecodeAngle14.
    /// </summary>
    private static uint EncodeAngle14(float angle)
    {
        const float twoPi = 2f * MathF.PI;
        const float divisor = 16384f;
        float raw = (angle + MathF.PI) * divisor / twoPi - 0.5f;
        return (uint)Math.Clamp((int)MathF.Round(raw), 0, 16383);
    }
}
