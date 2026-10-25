namespace SphServer.Helpers.Networking;

/// <summary>
///     The client's "take part of this stack into an empty cell" request, seen in a retail capture: region 4
///     command 4 of the container module, 9-byte payload
///     [u24 player id][u8 0][u8 destination cell][u8 source cell][u24 amount].
/// </summary>
public static class SplitStackFrame
{
    public readonly record struct Split(ushort ContainerId, int SourceCell, int DestinationCell, int Amount);

    private const int BodyBit = ClientFrame.BodyOffset * 8;
    private const int PositionBlockBits = 45;
    private const int WireContainerManager = 5;
    private const int SubjectTypePlayer = 2;
    private const int SplitCommand = 4;
    private const int SplitPayloadLength = 9;

    public static bool TryRead(ReadOnlySpan<byte> frame, out Split split)
    {
        split = default;
        if (frame.Length <= ClientFrame.BodyOffset)
        {
            return false;
        }

        var shift = ReadBits(frame, BodyBit, 1) == 1 ? PositionBlockBits : 0;
        var wireBit = BodyBit + shift + 46;
        if (wireBit + 7 > frame.Length * 8 || ReadBits(frame, wireBit, 7) != WireContainerManager ||
            ReadBits(frame, BodyBit + shift + 34, 12) == SubjectTypePlayer)
        {
            return false;
        }

        var commandBit = wireBit + 7;
        var lengthBit = commandBit + 8;
        var payloadBit = lengthBit + 8;
        if (payloadBit + SplitPayloadLength * 8 > frame.Length * 8 ||
            (int)ReadBits(frame, commandBit, 8) != SplitCommand ||
            (int)ReadBits(frame, lengthBit, 8) != SplitPayloadLength)
        {
            return false;
        }

        split = new Split((ushort)ReadBits(frame, BodyBit + shift + 16, 16),
            (int)ReadBits(frame, payloadBit + 5 * 8, 8), (int)ReadBits(frame, payloadBit + 4 * 8, 8),
            (int)ReadBits(frame, payloadBit + 6 * 8, 24));
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
