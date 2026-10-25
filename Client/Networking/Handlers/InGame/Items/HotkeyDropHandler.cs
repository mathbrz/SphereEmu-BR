using System.Threading.Tasks;
using SphServer.Helpers.Networking;
using SphServer.Packets;
using SphServer.Shared.Db;

namespace SphServer.Client.Networking.Handlers.InGame.Items;

/// <summary>
///     An item dropped on the shortcut window. The client lifts the item out of its cell for the drag
///     and keeps the cell empty until the server declares it again, as it does for the sale window.
///     Answering with the slot's real state puts the icon back; without it the item stays hidden
///     until the next login even though the server never moved it.
/// </summary>
public class HotkeyDropHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    public Task Handle(byte[] frame, double delta)
    {
        return HotkeyDropFrame.TryRead(frame, out var wireSlot) ? Declare(wireSlot) : Task.CompletedTask;
    }

    /// <summary>
    ///     The client packs this message behind a container move, so it reaches the server in the same
    ///     frame as the move. Called after that frame's own handler, so the slot is declared as it ends up.
    /// </summary>
    public Task HandleTrailing(byte[] frame)
    {
        return HotkeyDropFrame.TryReadTrailing(frame, out var wireSlot) ? Declare(wireSlot) : Task.CompletedTask;
    }

    private Task Declare(int wireSlot)
    {
        var character = clientConnection.GetSelectedCharacter();
        if (character is null || ItemSlotReserve.SlotForWireId(wireSlot) is not { } slot)
        {
            return Task.CompletedTask;
        }

        var held = character.Items.TryGetValue(slot, out var itemId) ? DbConnection.Items.FindById(itemId) : null;
        var declare = held is null
            ? ItemSlotReserve.Build(localId, slot, ItemSlotReserve.NoItem)
            : ItemSlotReserve.Build(localId, slot, held.Id, held.ItemCount);
        if (declare is not null)
        {
            clientConnection.MaybeScheduleNetworkPacketSend(declare);
        }

        return Task.CompletedTask;
    }
}
