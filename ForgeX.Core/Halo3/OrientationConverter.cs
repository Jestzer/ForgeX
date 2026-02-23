using ForgeX.Core.IO;

namespace ForgeX.Core.Halo3;

/// <summary>
/// Converts between forward/up vector pairs (MCC format) and yaw/pitch/roll (Xbox 360 format).
/// Also handles the Blam engine's compressed axis encoding used in packed mvar bitstreams.
/// </summary>
public static class OrientationConverter
{
    /// <summary>
    /// Converts forward and up vectors to yaw, pitch, roll (radians).
    /// </summary>
    public static (float Yaw, float Pitch, float Roll) ToYawPitchRoll(
        float forwardI, float forwardJ, float forwardK,
        float upI, float upJ, float upK)
    {
        float yaw = MathF.Atan2(forwardJ, forwardI);

        float clampedFk = Math.Clamp(-forwardK, -1f, 1f);
        float pitch = MathF.Asin(clampedFk);

        float cosPitch = MathF.Cos(pitch);
        float roll;
        if (MathF.Abs(cosPitch) < 1e-6f)
        {
            roll = 0f;
        }
        else
        {
            float sinYaw = MathF.Sin(yaw);
            float cosYaw = MathF.Cos(yaw);
            float upProj = upI * sinYaw - upJ * cosYaw;
            float upZ = upK;
            roll = MathF.Atan2(upProj, upZ);
        }

        return (yaw, pitch, roll);
    }

    /// <summary>
    /// Converts yaw, pitch, roll (radians) to forward and up vectors.
    /// </summary>
    public static (float ForwardI, float ForwardJ, float ForwardK,
                    float UpI, float UpJ, float UpK) ToForwardUp(float yaw, float pitch, float roll)
    {
        float cosYaw = MathF.Cos(yaw);
        float sinYaw = MathF.Sin(yaw);
        float cosPitch = MathF.Cos(pitch);
        float sinPitch = MathF.Sin(pitch);
        float cosRoll = MathF.Cos(roll);
        float sinRoll = MathF.Sin(roll);

        float forwardI = cosPitch * cosYaw;
        float forwardJ = cosPitch * sinYaw;
        float forwardK = -sinPitch;

        float upI = cosYaw * sinPitch * cosRoll + sinYaw * sinRoll;
        float upJ = sinYaw * sinPitch * cosRoll - cosYaw * sinRoll;
        float upK = cosPitch * cosRoll;

        return (forwardI, forwardJ, forwardK, upI, upJ, upK);
    }

    /// <summary>
    /// Dequantizes a 19-bit unit vector using the Blam engine's cube-face projection.
    /// Bits 0-2: face code (0-5), bits 3-10: u component, bits 11-18: v component.
    /// Each u/v is 8-bit quantized in [-1,1] with exact midpoint.
    /// </summary>
    public static (float I, float J, float K) DequantizeUnitVector3d(int value)
    {
        int face = value & 7;
        float x = BitReader.DequantizeReal((value >> 3) & 0xFF, -1f, 1f, 8, true);
        float y = BitReader.DequantizeReal((value >> 11) & 0xFF, -1f, 1f, 8, true);

        float i, j, k;
        switch (face)
        {
            case 0: i = 1f;  j = x;   k = y;   break;
            case 1: i = x;   j = 1f;  k = y;   break;
            case 2: i = x;   j = y;   k = 1f;  break;
            case 3: i = -1f; j = x;   k = y;   break;
            case 4: i = x;   j = -1f; k = y;   break;
            case 5: i = x;   j = y;   k = -1f; break;
            default: throw new ArgumentException($"Invalid face value {face}");
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
    /// Computes reference forward and left axes from an up vector.
    /// Used by the Blam engine's axis encoding to establish a reference frame.
    /// </summary>
    public static void AxesComputeReferenceInternal(
        float upI, float upJ, float upK,
        out float fwdI, out float fwdJ, out float fwdK,
        out float leftI, out float leftJ, out float leftK)
    {
        // global_forward3d = (1,0,0), global_left3d = (0,1,0)
        float dotForward = MathF.Abs(upI); // |dot(up, global_forward)|
        float dotLeft = MathF.Abs(upJ);    // |dot(up, global_left)|

        if (dotForward >= dotLeft)
        {
            // forward = cross(global_left, up) = cross((0,1,0), (upI,upJ,upK))
            fwdI = upK;
            fwdJ = 0;
            fwdK = -upI;
        }
        else
        {
            // forward = cross(up, global_forward) = cross((upI,upJ,upK), (1,0,0))
            fwdI = 0;
            fwdJ = upK;
            fwdK = -upJ;
        }

        // Normalize forward
        float fwdLen = MathF.Sqrt(fwdI * fwdI + fwdJ * fwdJ + fwdK * fwdK);
        if (fwdLen > 1e-6f)
        {
            fwdI /= fwdLen;
            fwdJ /= fwdLen;
            fwdK /= fwdLen;
        }

        // left = cross(up, forward)
        leftI = upJ * fwdK - upK * fwdJ;
        leftJ = upK * fwdI - upI * fwdK;
        leftK = upI * fwdJ - upJ * fwdI;

        // Normalize left
        float leftLen = MathF.Sqrt(leftI * leftI + leftJ * leftJ + leftK * leftK);
        if (leftLen > 1e-6f)
        {
            leftI /= leftLen;
            leftJ /= leftLen;
            leftK /= leftLen;
        }
    }

    /// <summary>
    /// Computes the forward vector from an up vector and a rotation angle.
    /// Matches the Blam engine's angle_to_axes_internal function.
    /// Uses Rodrigues' rotation to rotate the reference forward about the up axis.
    /// </summary>
    public static (float ForwardI, float ForwardJ, float ForwardK) AngleToAxesInternal(
        float upI, float upJ, float upK, float angle)
    {
        AxesComputeReferenceInternal(upI, upJ, upK,
            out float fwdI, out float fwdJ, out float fwdK,
            out float leftI, out float leftJ, out float leftK);

        float u, v;
        if (angle == MathF.PI || angle == -MathF.PI)
        {
            u = 0f;
            v = -1f;
        }
        else
        {
            u = MathF.Sin(angle);
            v = MathF.Cos(angle);
        }

        // Rodrigues' rotation: forward is perpendicular to up, so dot(up, forward) = 0
        // result = forward * cos(angle) + cross(up, forward) * sin(angle)
        //        = forward * v + left * u
        float resultI = fwdI * v + leftI * u;
        float resultJ = fwdJ * v + leftJ * u;
        float resultK = fwdK * v + leftK * u;

        // Normalize
        float len = MathF.Sqrt(resultI * resultI + resultJ * resultJ + resultK * resultK);
        if (len > 1e-6f)
        {
            resultI /= len;
            resultJ /= len;
            resultK /= len;
        }

        return (resultI, resultJ, resultK);
    }

    /// <summary>
    /// Reads forward and up axes from a packed mvar bitstream.
    /// Format: 1-bit up_is_global + optional 19-bit quantized up + 8-bit quantized forward angle.
    /// </summary>
    public static (float ForwardI, float ForwardJ, float ForwardK,
                    float UpI, float UpJ, float UpK) ReadAxes(BitReader bits)
    {
        float upI, upJ, upK;

        bool upIsGlobalUp = bits.ReadBool();
        if (upIsGlobalUp)
        {
            // global_up3d = (0, 0, 1)
            upI = 0f;
            upJ = 0f;
            upK = 1f;
        }
        else
        {
            int quantized = (int)bits.ReadInteger(19);
            (upI, upJ, upK) = DequantizeUnitVector3d(quantized);
        }

        // Forward angle: 8-bit quantized in [-pi, pi] with exact midpoint
        float forwardAngle = bits.ReadQuantizedReal(8, -MathF.PI, MathF.PI, true);
        var (fwdI, fwdJ, fwdK) = AngleToAxesInternal(upI, upJ, upK, forwardAngle);

        return (fwdI, fwdJ, fwdK, upI, upJ, upK);
    }

    /// <summary>
    /// Decodes a 19-bit compressed axis vector (legacy method kept for compatibility).
    /// </summary>
    public static (float I, float J, float K) DecodeCompressedAxis(uint encoded19Bits)
    {
        return DequantizeUnitVector3d((int)encoded19Bits);
    }

    /// <summary>
    /// Encodes a unit vector into 19-bit compressed format using cube-face projection.
    /// </summary>
    public static uint EncodeCompressedAxis(float i, float j, float k)
    {
        // Normalize
        float len = MathF.Sqrt(i * i + j * j + k * k);
        if (len > 1e-6f)
        {
            i /= len;
            j /= len;
            k /= len;
        }

        float absI = MathF.Abs(i);
        float absJ = MathF.Abs(j);
        float absK = MathF.Abs(k);

        int faceCode;
        float u, v;

        if (absI >= absJ && absI >= absK)
        {
            // X-dominant
            if (i > 0) { faceCode = 0; u = j / absI; v = k / absI; }
            else        { faceCode = 3; u = j / absI; v = k / absI; }
        }
        else if (absJ >= absI && absJ >= absK)
        {
            // Y-dominant
            if (j > 0) { faceCode = 1; u = i / absJ; v = k / absJ; }
            else        { faceCode = 4; u = i / absJ; v = k / absJ; }
        }
        else
        {
            // Z-dominant
            if (k > 0) { faceCode = 2; u = i / absK; v = j / absK; }
            else        { faceCode = 5; u = i / absK; v = j / absK; }
        }

        // Quantize u and v from [-1,1] to 8-bit with exact midpoint
        int stepCount = 254; // (1 << 8) - 1 = 255, adjusted for exact midpoint: 255 - (255 % 2) = 254
        int qu = (int)Math.Clamp(MathF.Round((u + 1f) / 2f * stepCount), 0, stepCount);
        int qv = (int)Math.Clamp(MathF.Round((v + 1f) / 2f * stepCount), 0, stepCount);

        return (uint)(faceCode | (qu << 3) | (qv << 11));
    }
}
