using System.Collections.Generic;
using System.Linq;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Networking;
using SphServer.Shared.WorldState;

namespace SphServer.Packets;

public static class ContainerLoadEncoder
{
    /// <summary>
    ///     The weight of a container and of everything it sits in, up to the character slot. The held
    ///     backpack carries the figure as a bare slot value; other slots name their item.
    /// </summary>
    public static IEnumerable<byte[]> Build(ushort localId, CharacterDbEntry character, ItemDbEntry container)
    {
        var current = container;
        for (var depth = 0; depth < 8 && current is not null; depth++)
        {
            if (current.HeldInItemId is { } parentId)
            {
                var parent = DbConnection.Items.FindById(parentId);
                if (parent is null)
                {
                    yield break;
                }

                yield return CommonPackets.BuildContainerCell(parent.Id, (ushort)parent.WireObjectType,
                    (byte)current.HeldInCell, current.Id, ItemContainers.WireWeight(current));
                current = parent;
                continue;
            }

            var held = character.Items.Where(x => x.Value == current.Id && x.Key != BelongingSlot.MainHand)
                .Select(x => (BelongingSlot?)x.Key).FirstOrDefault();
            if (held is { } slot && ItemSlotReserve.WireSlotId(slot) is { } wire)
            {
                yield return slot == BelongingSlot.Backpack
                    ? CommonPackets.BuildPlayerSlotValue(localId, (byte)wire, ItemContainers.WireWeight(current))
                    : CommonPackets.BuildPlayerCell(localId, (byte)wire, current.Id,
                        ItemContainers.WireWeight(current));
            }

            yield break;
        }
    }
}
