namespace SphServer.Helpers.Networking;

/// <summary>
///     The client's "add this stack to that one" request, seen in the retail captures and in ours as a UseItem
///     (region 4 command 6 of an item module is a move; command 7 is use) with a 9-byte payload
///     [u24 player id][u24 destination item id][u24 amount]. It is addressed to the dragged item. An ordinary use of
///     an item names the player twice, so a second id that is not the player's marks a stack.
/// </summary>
public static class StackItemFrame
{
    public readonly record struct Stack(int ItemId, int DestinationId, int Amount);

    private const int BodyBit = ClientFrame.BodyOffset * 8;
    private const int PositionBlockBits = 45;
    private const int WireContainerManager = 5;
    private const int SubjectTypePlayer = 2;
    private const int UseCommand = 7;
    private const int StackPayloadLength = 9;

    public static bool TryRead(ReadOnlySpan<byte> frame, out Stack stack)
    {
        stack = default;
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
        if (payloadBit + StackPayloadLength * 8 > frame.Length * 8 ||
            (int)ReadBits(frame, commandBit, 8) != UseCommand ||
            (int)ReadBits(frame, lengthBit, 8) != StackPayloadLength)
        {
            return false;
        }

        var player = (int)ReadBits(frame, payloadBit, 24);
        var destination = (int)ReadBits(frame, payloadBit + 3 * 8, 24);
        if (destination == 0 || destination == player)
        {
            return false;
        }

        var itemId = (int)ReadBits(frame, BodyBit + shift + 16, 16);
        stack = new Stack(itemId, destination, (int)ReadBits(frame, payloadBit + 6 * 8, 24));
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
