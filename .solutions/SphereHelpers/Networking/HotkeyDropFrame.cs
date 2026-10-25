namespace SphServer.Helpers.Networking;

/// <summary>
///     An item dropped on the shortcut window: region 4 (ContMan) command 5 addressed to the player,
///     u4 command and a 1-byte payload, the wire slot the item was lifted from. The shortcut itself lives
///     in the client's per-character file, nothing about it needs to be stored here.
/// </summary>
public static class HotkeyDropFrame
{
    private const int BodyBit = ClientFrame.BodyOffset * 8;
    private const int PositionBlockBits = 45;
    private const int WireContainerManager = 5;
    private const int SubjectTypePlayer = 2;
    private const int DropCommand = 5;
    private const int CommandBits = 4;
    private const int PayloadLength = 1;

    public static bool TryRead(ReadOnlySpan<byte> frame, out int wireSlot)
    {
        wireSlot = 0;
        if (frame.Length <= ClientFrame.BodyOffset)
        {
            return false;
        }

        var shift = ReadBits(frame, BodyBit, 1) == 1 ? PositionBlockBits : 0;
        var wireBit = BodyBit + shift + 46;
        var commandBit = wireBit + 7;
        var lengthBit = commandBit + CommandBits;
        var payloadBit = lengthBit + 8;
        if (payloadBit + PayloadLength * 8 > frame.Length * 8 ||
            ReadBits(frame, wireBit, 7) != WireContainerManager ||
            ReadBits(frame, BodyBit + shift + 34, 12) != SubjectTypePlayer ||
            (int)ReadBits(frame, commandBit, CommandBits) != DropCommand ||
            (int)ReadBits(frame, lengthBit, 8) != PayloadLength)
        {
            return false;
        }

        wireSlot = (int)ReadBits(frame, payloadBit, 8);
        return true;
    }

    /// <summary>
    ///     The same message when it rides behind another one in the frame, as it does after a refused
    ///     or accepted container move: a section switch (0x3F, process id, module) then the record.
    /// </summary>
    public static bool TryReadTrailing(ReadOnlySpan<byte> frame, out int wireSlot)
    {
        wireSlot = 0;
        const int sectionSwitch = 0x3F;
        const int span = 7 + 18 + 12 + 7 + CommandBits + 8 + 8;
        var total = frame.Length * 8;

        // The first message cannot end before the header block and its own marker.
        for (var bit = BodyBit + 46 + 7; bit + span <= total; bit++)
        {
            if (ReadBits(frame, bit, 7) == sectionSwitch &&
                ReadBits(frame, bit + 25, 12) == SubjectTypePlayer &&
                ReadBits(frame, bit + 37, 7) == WireContainerManager &&
                (int)ReadBits(frame, bit + 44, CommandBits) == DropCommand &&
                (int)ReadBits(frame, bit + 48, 8) == PayloadLength)
            {
                wireSlot = (int)ReadBits(frame, bit + 56, 8);
                return true;
            }
        }

        return false;
    }

    // LSB-first, matching the client's bit packing.
    private static ulong ReadBits(ReadOnlySpan<byte> data, int startBit, int width)
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
