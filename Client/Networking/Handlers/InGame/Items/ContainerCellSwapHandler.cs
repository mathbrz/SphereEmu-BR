using System.Threading.Tasks;
using SphServer.Helpers.Networking;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;
using SphServer.Shared.Networking;

namespace SphServer.Client.Networking.Handlers.InGame.Items;

/// <summary>
///     An item dragged from one cell of a carried bag, pouch or book to another cell of the same one.
///     The two cells swap, or the item moves when the target is free. The client lifts the dragged
///     item out of its cell, so both cells are declared again as they end up, which also puts the
///     item back when the move is impossible.
/// </summary>
public class ContainerCellSwapHandler(ClientConnection clientConnection) : ISphereClientNetworkingHandler
{
    public Task Handle(byte[] frame, double delta)
    {
        var character = clientConnection.GetSelectedCharacter();
        if (character is null || !ContainerSwapFrame.TryRead(frame, out var swap) ||
            DbConnection.Items.FindById((int)swap.ContainerId) is not { } container ||
            !ItemContainers.IsContainer(container) || ItemContainers.CarriedRoot(character, container) is null)
        {
            return Task.CompletedTask;
        }

        var capacity = ItemContainers.Capacity(container);
        var inRange = swap.CellA >= 0 && swap.CellB >= 0 && swap.CellA < capacity && swap.CellB < capacity;
        var atA = inRange ? ItemContainers.At(container.Id, swap.CellA) : null;
        var atB = inRange ? ItemContainers.At(container.Id, swap.CellB) : null;
        SphLogger.Info($"Container: cell request {swap.CellA},{swap.CellB} in {container.Id:X4} " +
                       $"(A={atA?.Id.ToString("X4") ?? "-"} B={atB?.Id.ToString("X4") ?? "-"}, capacity {capacity}). " +
                       $"Client ID: {clientConnection.LocalId:X4}");

        // Retail never sent this command, so which byte is the source is not known: whichever cell holds an
        // item moves to the other one, and two items swap.
        if (inRange && swap.CellA != swap.CellB && (atA is not null || atB is not null))
        {
            if (atA is not null)
            {
                atA.HeldInCell = swap.CellB;
                DbConnection.Items.Update(atA);
            }

            if (atB is not null)
            {
                atB.HeldInCell = swap.CellA;
                DbConnection.Items.Update(atB);
            }
        }

        Declare(container, swap.CellA, capacity);
        Declare(container, swap.CellB, capacity);
        return Task.CompletedTask;
    }

    private void Declare(ItemDbEntry container, int cell, int capacity)
    {
        if (cell < 0 || cell >= capacity)
        {
            return;
        }

        var type = (ushort)container.WireObjectType;
        var item = ItemContainers.At(container.Id, cell);
        clientConnection.MaybeScheduleNetworkPacketSend(item is null
            ? CommonPackets.BuildContainerCellRelease(container.Id, type, (byte)cell)
            : CommonPackets.BuildContainerCell(container.Id, type, (byte)cell, item.Id,
                ItemContainers.WireWeight(item)));
    }
}
