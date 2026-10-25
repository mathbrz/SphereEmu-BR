using System;
using System.Collections.Generic;
using System.Linq;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Networking;
using SphServer.Shared.WorldState;

namespace SphServer.Packets;

/// <summary>
///     Everything the client needs to know about what a character carries: the cell table, each item
///     record with its container contents, and the empty cells. Sent at login and, after the clears of
///     BuildAfterDeath, once the respawn has reloaded the zone.
/// </summary>
public static class CharacterItemDeclaration
{
    private static readonly BelongingSlot[] EmptySlotsToDeclare = Enum.GetValues<BelongingSlot>()
        .Where(x => x is not (BelongingSlot.Money or BelongingSlot.MainHand or BelongingSlot.Unknown
                          or BelongingSlot.TokenIsland or BelongingSlot.Ammo or BelongingSlot.SpeedhackMantra)
                    && !x.ToString().StartsWith("Mutator_") && !x.ToString().StartsWith("Special_"))
        .ToArray();

    /// <summary>Login order: for each slot, its cell then its record, then the empty cells.</summary>
    public static IEnumerable<byte[]> Build(ushort localId, CharacterDbEntry character)
    {
        yield return CommonPackets.BuildPlayerClearSlots(localId);

        var declared = new HashSet<int>();

        foreach (var (slot, itemId) in character.Items)
        {
            // MainHand is a reference to a carried item, not its inventory cell
            if (slot == BelongingSlot.MainHand)
            {
                continue;
            }

            var item = DbConnection.Items.FindById(itemId);
            if (item is null || !declared.Add(itemId))
            {
                continue;
            }

            var record = ItemRecordEncoder.Encode(item, SphServer.Shared.BitStream.SphBitStream.ByteSwap(localId));

            if (slot == BelongingSlot.Helmet)
            {
                yield return record;
                yield return ItemSlotReserve.BuildSlotBinding(localId, slot, item.Id);
                continue;
            }

            var reserve = ItemSlotReserve.Build(localId, slot, item.Id, item.ItemCount);
            if (reserve is not null)
            {
                yield return reserve;
            }

            yield return record;

            foreach (var held in ContainerContentsEncoder.Build(item))
            {
                yield return held;
            }

            if (slot == BelongingSlot.Backpack && ItemContainers.HasContents(item) &&
                ItemSlotReserve.WireSlotId(slot) is { } backpackWire)
            {
                yield return CommonPackets.BuildPlayerSlotValue(localId, (byte)backpackWire,
                    ItemContainers.WireWeight(item));
            }
        }

        // Retail declares the empty cells too (item 0). The client only offers a cell as a drop target,
        // for unequipping or moving into the bag, once the server has named it.
        foreach (var slot in EmptySlotsToDeclare.Where(x => !character.Items.ContainsKey(x)))
        {
            var empty = ItemSlotReserve.Build(localId, slot, ItemSlotReserve.NoItem);
            if (empty is not null)
            {
                yield return empty;
            }
        }
    }

    /// <summary>
    ///     Retail's respawn: clear every container and the player's cells (flag 1), then declare it all
    ///     anew. Sent after the zone reload, which takes the client's objects with it.
    /// </summary>
    public static IEnumerable<byte[]> BuildAfterDeath(ushort localId, CharacterDbEntry character)
    {
        foreach (var container in CarriedContainers(character))
        {
            yield return CommonPackets.BuildContainerClear(container.Id, (ushort)container.WireObjectType, 1);
        }

        yield return CommonPackets.BuildPlayerClearSlots(localId, 1);

        foreach (var packet in Build(localId, character))
        {
            yield return packet;
        }
    }

    private static IEnumerable<ItemDbEntry> CarriedContainers(CharacterDbEntry character)
    {
        foreach (var itemId in character.Items.Values.Distinct())
        {
            if (DbConnection.Items.FindById(itemId) is not { } root || !ItemContainers.IsContainer(root))
            {
                continue;
            }

            yield return root;
            foreach (var nested in ItemContainers.Descendants(root).Where(ItemContainers.IsContainer))
            {
                yield return nested;
            }
        }
    }
}
