using System;

namespace SphServer.Shared.Networking;

/// <summary>
///     Reads the first _player Manager record (wire 13: u8 command, u8 length, payload) out of a
///     de-obfuscated client frame. The record starts after the 1-bit position flag, the optional
///     45-bit position block, and the 15 + 18 + 12 bit clock, process id and module tag.
/// </summary>
public static class ManagerPayload
{
    private const int BodyBit = 9 * 8;
    private const int PositionBlockBits = 45;
    private const int WireManager = 13;

    public static bool TryRead(byte[] frame, out byte command, out byte[] payload)
    {
        command = 0;
        payload = [];

        if (frame.Length <= 9)
        {
            return false;
        }

        var shift = ReadBits(frame, BodyBit, 1) == 1 ? PositionBlockBits : 0;
        var wireBit = BodyBit + shift + 46;
        if (wireBit + 7 + 16 > frame.Length * 8 || ReadBits(frame, wireBit, 7) != WireManager)
        {
            return false;
        }

        command = (byte)ReadBits(frame, wireBit + 7, 8);
        var length = (int)ReadBits(frame, wireBit + 15, 8);
        var start = wireBit + 23;
        if (start + length * 8 > frame.Length * 8)
        {
            return false;
        }

        payload = new byte[length];
        for (var i = 0; i < length; i++)
        {
            payload[i] = (byte)ReadBits(frame, start + i * 8, 8);
        }

        return true;
    }

    public static int U24(byte[] payload, int offset) =>
        payload[offset] | (payload[offset + 1] << 8) | (payload[offset + 2] << 16);

    public static uint U32(byte[] payload, int offset) => BitConverter.ToUInt32(payload, offset);

    // LSB-first, matching the client's bit packing.
    private static ulong ReadBits(byte[] data, int startBit, int width)
    {
        ulong value = 0;
        for (var i = 0; i < width; i++)
        {
            var bit = startBit + i;
            if ((data[bit >> 3] & (1 << (bit & 7))) != 0)
            {
                value |= 1UL << i;
            }
        }

        return value;
    }
}
