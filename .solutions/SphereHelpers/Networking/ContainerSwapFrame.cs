namespace SphServer.Helpers.Networking;

/// <summary>
///     Dragging an item from one cell of a bag, pouch or book to another cell of the same container:
///     region 4 command 4 with a 6-byte payload [u24 player id][u8 0][u8 cell a][u8 cell b], addressed to
///     the container (u8 command, like every item module). On an item container the MBC swaps the two
///     cells and answers with both of them, unlike the player's own command 4 which only echoes a slot.
/// </summary>
public static class ContainerSwapFrame
{
    public readonly record struct Swap(ushort ContainerId, int CellA, int CellB);

    private const int BodyBit = ClientFrame.BodyOffset * 8;
    private const int PositionBlockBits = 45;
    private const int WireContainerManager = 5;
    private const int SubjectTypePlayer = 2;
    private const int SwapCommand = 4;
    private const int SwapPayloadLength = 6;

    public static bool TryRead(ReadOnlySpan<byte> frame, out Swap swap)
    {
        swap = default;
        if (frame.Length <= ClientFrame.BodyOffset)
        {
            return false;
        }

        var shift = ReadBits(frame, BodyBit, 1) == 1 ? PositionBlockBits : 0;
        var wireBit = BodyBit + shift + 46;
        var commandBit = wireBit + 7;
        var lengthBit = commandBit + 8;
        var payloadBit = lengthBit + 8;
        if (payloadBit + SwapPayloadLength * 8 > frame.Length * 8 ||
            ReadBits(frame, wireBit, 7) != WireContainerManager ||
            ReadBits(frame, BodyBit + shift + 34, 12) == SubjectTypePlayer ||
            (int)ReadBits(frame, commandBit, 8) != SwapCommand ||
            (int)ReadBits(frame, lengthBit, 8) != SwapPayloadLength)
        {
            return false;
        }

        var containerId = (ushort)ReadBits(frame, BodyBit + shift + 16, 16);
        swap = new Swap(containerId, (int)ReadBits(frame, payloadBit + 4 * 8, 8),
            (int)ReadBits(frame, payloadBit + 5 * 8, 8));
        return true;
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
