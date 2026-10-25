namespace SphServer.Helpers.Networking;

/// <summary>
///     The client's "put this item in that cell" request: region 4 command 6 with an 8-byte payload
///     [u24 player id][u8 0][u24 item id][u8 cell]. It is addressed to the DESTINATION: to the
///     container when the item goes into one (u8 command), to the player when it comes out onto a
///     slot (u4 command), and the cell byte is then the wire slot id.
/// </summary>
public static class ContainerMoveFrame
{
    public readonly record struct Move(ushort TargetId, bool ToPlayer, int ItemId, int Cell);

    private const int BodyBit = ClientFrame.BodyOffset * 8;
    private const int PositionBlockBits = 45;
    private const int WireContainerManager = 5;
    private const int SubjectTypePlayer = 2;
    private const int MoveCommand = 6;
    private const int MovePayloadLength = 8;

    public static bool TryRead(ReadOnlySpan<byte> frame, out Move move)
    {
        move = default;
        if (frame.Length <= ClientFrame.BodyOffset)
        {
            return false;
        }

        var shift = ReadBits(frame, BodyBit, 1) == 1 ? PositionBlockBits : 0;
        var wireBit = BodyBit + shift + 46;
        if (wireBit + 7 > frame.Length * 8 || ReadBits(frame, wireBit, 7) != WireContainerManager)
        {
            return false;
        }

        var targetId = (ushort)ReadBits(frame, BodyBit + shift + 16, 16);
        var toPlayer = ReadBits(frame, BodyBit + shift + 34, 12) == SubjectTypePlayer;

        // The player's ContMan commands are u4, every item module's are u8.
        var commandBits = toPlayer ? 4 : 8;
        var commandBit = wireBit + 7;
        var lengthBit = commandBit + commandBits;
        var payloadBit = lengthBit + 8;
        if (payloadBit + MovePayloadLength * 8 > frame.Length * 8 ||
            (int)ReadBits(frame, commandBit, commandBits) != MoveCommand ||
            (int)ReadBits(frame, lengthBit, 8) != MovePayloadLength)
        {
            return false;
        }

        var itemId = (int)ReadBits(frame, payloadBit + 4 * 8, 24);
        var cell = (int)ReadBits(frame, payloadBit + 7 * 8, 8);
        move = new Move(targetId, toPlayer, itemId, cell);
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
