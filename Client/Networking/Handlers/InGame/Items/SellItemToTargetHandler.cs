using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SphServer.Packets;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;
using SphServer.Shared.Networking;

namespace SphServer.Client.Networking.Handlers.InGame.Items;

/// <summary>
///     Manager cmd 6 (SetTrade) from the client, one message per sale-window slot. Offering an item
///     (len 12: u8 sale slot, u24 item id, u32 count, u32 0xFFFFFFFF) gets the trader's price back as
///     cmd 7. Accepting it (len 9: u8 sale slot, u32 count, u32 price) closes that slot's sale and pays
///     out. A lone slot byte (len 1) is the player taking the item back out of the window.
/// </summary>
public class SellItemToTargetHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    private const int OfferLength = 12;
    private const int AcceptLength = 9;
    private const int CancelLength = 1;

    // Retail pays a tenth of the vendor price (120 for the recipe book buys back at 12).
    private const int BuybackDivisor = 10;

    private readonly record struct SaleOffer(int ItemId, int UnitPrice, int Quantity);

    /// <summary>A sellable item and where it sits: an inventory cell, or a cell of a carried bag or pouch.</summary>
    private readonly record struct SaleItem(ItemDbEntry Item, BelongingSlot? Slot, ItemDbEntry? Container);

    private readonly Dictionary<byte, SaleOffer> offers = new();

    public Task Handle(byte[] frame, double delta)
    {
        if (!ManagerPayload.TryRead(frame, out var command, out var payload) || command != 6)
        {
            return Task.CompletedTask;
        }

        var character = clientConnection.GetSelectedCharacter();
        var vendorId = clientConnection.ActiveTradeVendorId;
        if (character is null || vendorId == 0)
        {
            return Task.CompletedTask;
        }

        switch (payload.Length)
        {
            case OfferLength:
                Offer(character, payload, vendorId);
                break;
            case AcceptLength:
                Accept(character, payload, vendorId);
                break;
            case CancelLength:
                offers.Remove(payload[0]);
                clientConnection.MaybeScheduleNetworkPacketSend(
                    CommonPackets.BuildPlayerTradeOffer(localId, payload[0], uint.MaxValue, traderId: null));
                break;
            default:
                SphLogger.Info($"Sell: SetTrade of {payload.Length} bytes: {Convert.ToHexString(payload)}. " +
                               $"Client ID: {localId:X4}");
                break;
        }

        return Task.CompletedTask;
    }

    // How many units of the offered stack the player is selling: the count the client names, else all.
    private static int Quantity(uint requested, ItemDbEntry item) =>
        requested == 0 ? Math.Max(1, item.ItemCount) : (int)Math.Min(requested, (uint)Math.Max(1, item.ItemCount));

    // Worn items and anything pasted into a book are not for sale, a bag's contents are.
    private static SaleItem? Find(CharacterDbEntry character, int itemId)
    {
        if (DbConnection.Items.FindById(itemId) is not { } item)
        {
            return null;
        }

        var held = character.Items.Where(x => x.Value == itemId && ItemDbEntry.IsInventorySlot(x.Key))
            .Select(x => (BelongingSlot?)x.Key).FirstOrDefault();
        if (held is not null)
        {
            return new SaleItem(item, held, null);
        }

        if (item.HeldInItemId is not { } parentId || ItemContainers.CarriedRoot(character, item) is null ||
            DbConnection.Items.FindById(parentId) is not { } container)
        {
            return null;
        }

        var isBook = container.HomeSlot is { } home && !ItemDbEntry.IsInventorySlot(home) &&
                     home != BelongingSlot.Backpack;
        return isBook ? null : new SaleItem(item, null, container);
    }

    private void Offer(CharacterDbEntry character, byte[] payload, ushort vendorId)
    {
        var slot = payload[0];
        var found = Find(character, ManagerPayload.U24(payload, 1));

        // The client lifts the item out of its cell when it is dragged to the sale window. Retail declares
        // the cell again, so the item stays where it was until the sale is accepted.
        if (found is { } located)
        {
            var reserve = located.Container is { } bag
                ? CommonPackets.BuildContainerCell(bag.Id, (ushort)bag.WireObjectType, (byte)located.Item.HeldInCell,
                    located.Item.Id, ItemContainers.WireWeight(located.Item))
                : ItemSlotReserve.Build(localId, located.Slot!.Value, located.Item.Id, located.Item.ItemCount);
            if (reserve is not null)
            {
                clientConnection.MaybeScheduleNetworkPacketSend(reserve);
            }
        }

        // A container with contents stays: selling it would orphan what is inside.
        var item = found?.Item;
        if (item is null || item.VendorCost <= 0 || ItemContainers.HasContents(item))
        {
            offers.Remove(slot);
            clientConnection.MaybeScheduleNetworkPacketSend(
                CommonPackets.BuildPlayerTradeOffer(localId, slot, uint.MaxValue, traderId: null));
            return;
        }

        var quantity = Quantity(ManagerPayload.U32(payload, 4), item);
        var unitPrice = Math.Max(1, item.VendorCost / BuybackDivisor);
        offers[slot] = new SaleOffer(item.Id, unitPrice, quantity);
        SphLogger.Info($"Sell: slot {slot} offer for [{item.GameId}] x{quantity} of {item.ItemCount}, " +
                       $"{unitPrice} each. Client ID: {localId:X4}");
        clientConnection.MaybeScheduleNetworkPacketSend(
            CommonPackets.BuildPlayerTradeOffer(localId, slot, (uint)(unitPrice * quantity), vendorId));
    }

    private void Accept(CharacterDbEntry character, byte[] payload, ushort vendorId)
    {
        var slot = payload[0];
        var price = ManagerPayload.U32(payload, 5);
        var cleared = CommonPackets.BuildPlayerTradeOffer(localId, slot, uint.MaxValue, traderId: null);

        if (!offers.Remove(slot, out var offer))
        {
            SphLogger.Warning($"Sell: slot {slot} has no offer to accept (price {price}). Client ID: {localId:X4}");
            clientConnection.MaybeScheduleNetworkPacketSend(cleared);
            return;
        }

        var found = Find(character, offer.ItemId);
        var item = found?.Item;
        var quantity = item is null ? 0 : Math.Min(offer.Quantity, Math.Max(1, item.ItemCount));
        if (found is not { } sold || item is null || price != (uint)(offer.UnitPrice * quantity))
        {
            SphLogger.Warning($"Sell: slot {slot} cannot be accepted (item {offer.ItemId}, price {price}, " +
                              $"{offer.UnitPrice} each, x{quantity}). Client ID: {localId:X4}");
            clientConnection.MaybeScheduleNetworkPacketSend(cleared);
            return;
        }

        var wholeStack = quantity >= item.ItemCount;
        var remaining = item.ItemCount - quantity;
        var cell = item.HeldInCell;
        if (wholeStack)
        {
            if (sold.Slot is { } soldSlot)
            {
                character.Items.Remove(soldSlot);
            }

            DbConnection.Items.Delete(item.Id);
        }
        else
        {
            item.ItemCount = remaining;
            DbConnection.Items.Update(item);
        }

        character.Money += (int)price;
        clientConnection.SaveSelectedCharacter();

        clientConnection.MaybeScheduleNetworkPacketSend(cleared);
        foreach (var update in CellUpdate(character, sold, item, cell, wholeStack, remaining))
        {
            clientConnection.MaybeScheduleNetworkPacketSend(update);
        }

        clientConnection.MaybeScheduleNetworkPacketSend(
            CommonPackets.BuildPlayerSaleConfirm(localId, item.Id, (int)price, vendorId,
                (ushort)item.WireObjectType));
        if (!wholeStack)
        {
            clientConnection.MaybeScheduleNetworkPacketSend(
                CommonPackets.BuildItemAmount((ushort)item.Id, (ushort)item.WireObjectType, (ushort)remaining));
        }

        if (wholeStack)
        {
            clientConnection.MaybeScheduleNetworkPacketSend(
                CommonPackets.BuildItemHide((ushort)item.Id, (ushort)item.WireObjectType));
        }

        clientConnection.MaybeScheduleNetworkPacketSend(CommonPackets.BuildPlayerMoney(localId, character.Money));

        var from = sold.Container is null ? sold.Slot.ToString() : $"bag cell {cell}";
        SphLogger.Info($"Sell: slot {slot} [{item.GameId}] x{quantity} for {price} to [{vendorId:X4}] from " +
                       $"{from}, {(wholeStack ? "stack gone" : remaining + " left")}. Client ID: {localId:X4}");
    }

    // What the client needs to see the cell empty, or the stack smaller, after the sale.
    private IEnumerable<byte[]> CellUpdate(CharacterDbEntry character, SaleItem sold, ItemDbEntry item, int cell,
        bool wholeStack, int remaining)
    {
        if (sold.Container is { } bag)
        {
            yield return wholeStack
                ? CommonPackets.BuildContainerCellRelease(bag.Id, (ushort)bag.WireObjectType, (byte)cell)
                : CommonPackets.BuildContainerCell(bag.Id, (ushort)bag.WireObjectType, (byte)cell, item.Id,
                    ItemContainers.WireWeight(item));

            foreach (var load in ContainerLoadEncoder.Build(localId, character, bag))
            {
                yield return load;
            }

            yield break;
        }

        var soldSlot = sold.Slot!.Value;
        var wireSlot = ItemSlotReserve.WireSlotId(soldSlot);
        var update = wholeStack
            ? ItemSlotReserve.Build(localId, soldSlot, ItemSlotReserve.NoItem)
            : wireSlot is { } wire ? CommonPackets.BuildPlayerSlotCount(localId, (byte)wire, (ushort)remaining) : null;
        if (update is not null)
        {
            yield return update;
        }
    }
}
