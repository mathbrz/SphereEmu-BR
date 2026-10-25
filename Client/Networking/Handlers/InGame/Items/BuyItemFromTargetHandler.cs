using System;
using System.Linq;
using System.Threading.Tasks;
using SphServer.Packets;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;
using SphServer.Shared.Networking;
using SphServer.Shared.WorldState;
using SphServer.Sphere.Game.WorldObject;
using static SphServer.Shared.BitStream.SphBitStream;

namespace SphServer.Client.Networking.Handlers.InGame.Items;

/// <summary>
///     Manager cmd 8 from the client: u24 vendor id, u24 shop slot, u32 unit price, u32 count. The slot is the
///     number the shop list gave the row, which our list starts at 1.
///     Retail answers with the slot binding, the new balance, a purchase notice and the item record.
/// </summary>
public class BuyItemFromTargetHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    private const int BuyPayloadLength = 14;

    public Task Handle(byte[] frame, double delta)
    {
        if (!ManagerPayload.TryRead(frame, out var command, out var payload) ||
            command != 8 || payload.Length != BuyPayloadLength)
        {
            return Task.CompletedTask;
        }

        var vendorId = (ushort)ManagerPayload.U24(payload, 0);
        var listSlot = ManagerPayload.U24(payload, 3);
        var row = listSlot - 1;
        var count = (int)Math.Clamp(ManagerPayload.U32(payload, 10), 1u, 9999u);

        var character = clientConnection.GetSelectedCharacter();
        if (character is null || ActiveWorldObjects.Get(vendorId) is not NpcInteractable vendor)
        {
            SphLogger.Warning($"Buy: no character or vendor [{vendorId:X4}]. Client ID: {localId:X4}");
            return Task.CompletedTask;
        }

        if (row < 0 || row >= vendor.GetMaxItemsOnSale())
        {
            SphLogger.Warning($"Buy: vendor [{vendorId:X4}] has no slot {listSlot}. Client ID: {localId:X4}");
            return Task.CompletedTask;
        }

        var template = vendor.ItemsOnSale[row];
        var total = (long)template.VendorCost * count;
        if (total > character.Money)
        {
            SphLogger.Info($"Buy: [{total}] is more than the purse [{character.Money}]. Client ID: {localId:X4}");
            clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildPlayerMoney(localId, character.Money));
            return Task.CompletedTask;
        }

        // Stock listed in bulk (powders, arrows) stacks onto what the character already carries.
        var stack = template.ItemCount > 1 ? FindStack(character, template) : null;
        if (stack is { } existing)
        {
            Restock(character, existing, template, count, total, vendorId);
            return Task.CompletedTask;
        }

        // A bought bag goes straight to the bag slot, as in retail, when that slot is free.
        var goesToBagSlot = template.WireObjectType is ObjectType.Backpack_Small or ObjectType.Backpack_Large
                            && !character.Items.ContainsKey(BelongingSlot.Backpack);
        // Retail puts a purchase in the first free cell of the carried bag when there is one.
        var bag = goesToBagSlot ? null : CarriedBag(character);
        var bagCell = bag is null ? null : ItemContainers.FirstFreeCell(bag);
        BelongingSlot? slot = goesToBagSlot ? BelongingSlot.Backpack
            : bagCell is null ? character.FindEmptyInventorySlot() : null;
        if (slot is null && bagCell is null)
        {
            SphLogger.Info($"Buy: no free inventory slot. Client ID: {localId:X4}");
            return Task.CompletedTask;
        }

        var item = ItemDbEntry.Clone(template);
        item.ItemCount = count;
        item.ParentContainerId = null;
        item.HeldInItemId = bagCell is null ? null : bag!.Id;
        item.HeldInCell = bagCell ?? 0;
        DbConnection.Items.Update(item);

        character.Money -= (int)total;
        if (slot is { } worn)
        {
            character.Items[worn] = item.Id;
        }

        clientConnection.SaveSelectedCharacter();

        if (bagCell is { } cell)
        {
            clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildContainerCell(bag!.Id,
                (ushort)bag.WireObjectType, (byte)cell, item.Id, ItemContainers.WireWeight(item)));
            foreach (var load in ContainerLoadEncoder.Build(localId, character, bag))
            {
                clientConnection.MaybeScheduleNetworkPacketSend(load);
            }
        }
        else
        {
            var reserve = ItemSlotReserve.Build(localId, slot!.Value, item.Id, item.ItemCount);
            if (reserve is not null)
            {
                clientConnection.MaybeScheduleNetworkPacketSend(reserve);
            }
        }

        clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildPlayerMoney(localId, character.Money));
        clientConnection.MaybeScheduleNetworkPacketSend(
            CommonPackets.BuildPlayerPurchaseNotice(localId, item.Id, template.VendorCost, count));
        var parentId = bagCell is null ? ByteSwap(localId) : (ushort)bag!.Id;
        clientConnection.MaybeScheduleNetworkPacketSend(ItemRecordEncoder.Encode(item, parentId));

        SphLogger.Info($"Buy: [{item.GameId}] x{count} for {total} from [{vendorId:X4}] into " +
                       $"{(bagCell is null ? slot!.Value.ToString() : $"bag cell {bagCell}")}. Client ID: {localId:X4}");
        return Task.CompletedTask;
    }

    private static ItemDbEntry? CarriedBag(CharacterDbEntry character) =>
        character.Items.TryGetValue(BelongingSlot.Backpack, out var bagId) ? DbConnection.Items.FindById(bagId) : null;

    // A stack may sit in an inventory cell or anywhere inside a carried container.
    private static ItemDbEntry? FindStack(CharacterDbEntry character, ItemDbEntry template)
    {
        foreach (var (slot, itemId) in character.Items)
        {
            if (DbConnection.Items.FindById(itemId) is not { } root)
            {
                continue;
            }

            if (ItemDbEntry.IsInventorySlot(slot) && SameStack(root, template))
            {
                return root;
            }

            if (ItemContainers.IsContainer(root) &&
                ItemContainers.Descendants(root).FirstOrDefault(x => SameStack(x, template)) is { } inside)
            {
                return inside;
            }
        }

        return null;
    }

    private static bool SameStack(ItemDbEntry held, ItemDbEntry template) =>
        held.GameId == template.GameId && Equals(held.Suffix, template.Suffix);

    private void Restock(CharacterDbEntry character, ItemDbEntry stack, ItemDbEntry template, int count,
        long total, ushort vendorId)
    {
        var grown = stack.ItemCount + count;
        var parent = stack.HeldInItemId is { } parentId ? DbConnection.Items.FindById(parentId) : null;
        var inventorySlot = parent is null
            ? character.Items.Where(x => x.Value == stack.Id).Select(x => (BelongingSlot?)x.Key).FirstOrDefault()
            : null;
        var wireSlot = inventorySlot is { } held ? ItemSlotReserve.WireSlotId(held) : null;
        if (grown > ushort.MaxValue || (parent is null && wireSlot is null))
        {
            SphLogger.Info($"Buy: cannot grow the stack of [{stack.GameId}]. Client ID: {localId:X4}");
            return;
        }

        stack.ItemCount = grown;
        DbConnection.Items.Update(stack);
        character.Money -= (int)total;
        clientConnection.SaveSelectedCharacter();

        if (parent is not null)
        {
            clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildContainerCell(parent.Id,
                (ushort)parent.WireObjectType, (byte)stack.HeldInCell, stack.Id, ItemContainers.WireWeight(stack)));
            foreach (var load in ContainerLoadEncoder.Build(localId, character, parent))
            {
                clientConnection.MaybeScheduleNetworkPacketSend(load);
            }
        }
        else
        {
            clientConnection.MaybeScheduleNetworkPacketSend(
                CommonPackets.BuildPlayerSlotCount(localId, (byte)wireSlot!.Value, (ushort)grown));
        }

        clientConnection.MaybeScheduleNetworkPacketSend(
            CommonPackets.BuildItemAmount((ushort)stack.Id, (ushort)stack.WireObjectType, (ushort)grown));
        clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildPlayerMoney(localId, character.Money));
        clientConnection.MaybeScheduleNetworkPacketSend(
            CommonPackets.BuildPlayerPurchaseNotice(localId, stack.Id, template.VendorCost, count));

        SphLogger.Info($"Buy: [{stack.GameId}] x{count} for {total} from [{vendorId:X4}] onto the stack, now " +
                       $"{grown}. Client ID: {localId:X4}");
    }
}
