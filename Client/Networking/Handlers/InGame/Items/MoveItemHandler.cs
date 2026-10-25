using System;
using System.Threading.Tasks;
using SphServer.Client.Networking.GameplayLogic.Stats;
using SphServer.Packets;
using SphServer.Server;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;
using SphServer.Shared.BitStream;
using SphServer.Shared.Networking;
using SphServer.Shared.Networking.Chat.Encoders;
using SphServer.Shared.WorldState;
using static SphServer.Shared.Networking.DataModel.Serializers.SphereDbEntrySerializerBase;

namespace SphServer.Client.Networking.Handlers.InGame.Items;

public class MoveItemHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    /// <summary>
    /// Payload of player ContMan cmd 5 len 1: the cell number, as in the slot-set message
    /// </summary>
    private const int LeftCellBitOffset = 137;

    /// <summary>
    /// Long enough for a real move's own request to land first
    /// </summary>
    private const double RestoreCellAfterSeconds = 0.5;

    /// <summary>
    /// The inventory window empties a cell on its own when a drag from it ends elsewhere (_inv
    /// DnDRes 1), e.g. on a hotkey, which only keeps a reference: the item never left the server's
    /// slot, so the cell is put back unless a real move took the item away meanwhile
    /// </summary>
    public Task HandleLeftCell (byte[] frame, double delta)
    {
        var stream = new BitStreams.BitStream (frame);
        stream.ReadBits (LeftCellBitOffset);
        var slot = ItemSlotReserve.SlotForWireId (stream.ReadByte (8)) ?? BelongingSlot.Unknown;
        var character = clientConnection.GetSelectedCharacter ();
        if (character is null || !character.Items.TryGetValue (slot, out var itemId))
        {
            SphLogger.Info ($"Cell {Enum.GetName (slot) ?? slot.ToString ()} let go of nothing. Client ID: {localId:X4}");
            return Task.CompletedTask;
        }

        SphereServer.ServerNode.GetTree ().CreateTimer (RestoreCellAfterSeconds).Timeout += () =>
        {
            if (!ReferenceEquals (clientConnection.GetSelectedCharacter (), character))
            {
                return;
            }

            if (!character.Items.TryGetValue (slot, out var stillThere) || stillThere != itemId)
            {
                return;
            }

            var item = DbConnection.Items.FindById (itemId);
            if (ItemSlotReserve.Build (localId, slot, itemId, item?.ItemCount ?? 1) is { } reserve)
            {
                clientConnection.MaybeScheduleNetworkPacketSend (reserve);
            }

            SphLogger.Info ($"Cell {Enum.GetName (slot)} restored with {itemId:X4} after the client let go of it. " +
                            $"Client ID: {localId:X4}");
        };
        return Task.CompletedTask;
    }

    private const int AmountByte = 23;

    /// <summary>
    /// Dragging part of a stack into an empty inventory cell: the rest stays where it was. Retail's own split
    /// frame is not in our captures; the amount sits where the move frame has it (seen for 1 and 3).
    /// </summary>
    private bool SplitStack (CharacterDbEntry character, BelongingSlot from, BelongingSlot to, int amount)
    {
        if (amount < 1 || from == to || !ItemDbEntry.IsInventorySlot (from) || !ItemDbEntry.IsInventorySlot (to) ||
            !character.IsItemSlotEmpty (to) || !character.Items.TryGetValue (from, out var itemId) ||
            DbConnection.Items.FindById (itemId) is not { } stack || stack.ItemCount <= amount ||
            ItemSlotReserve.WireSlotId (from) is null)
        {
            return false;
        }

        var part = ItemDbEntry.Clone (stack);
        part.ItemCount = amount;
        part.ParentContainerId = null;
        part.HeldInItemId = null;
        part.HeldInCell = 0;
        DbConnection.Items.Update (part);

        stack.ItemCount -= amount;
        DbConnection.Items.Update (stack);
        character.Items[to] = part.Id;
        clientConnection.SaveSelectedCharacter ();

        // The window empties the dragged-from cell on its own, so the cell is set again, not just recounted.
        if (ItemSlotReserve.Build (localId, from, stack.Id, stack.ItemCount) is { } source)
        {
            clientConnection.MaybeScheduleNetworkPacketSend (source);
        }

        clientConnection.MaybeScheduleNetworkPacketSend (
            CommonPackets.BuildItemAmount ((ushort) stack.Id, (ushort) stack.WireObjectType, (ushort) stack.ItemCount));
        if (ItemSlotReserve.Build (localId, to, part.Id, part.ItemCount) is { } reserve)
        {
            clientConnection.MaybeScheduleNetworkPacketSend (reserve);
        }

        clientConnection.MaybeScheduleNetworkPacketSend (
            ItemRecordEncoder.Encode (part, SphBitStream.ByteSwap (localId)));
        SphLogger.Info ($"Split: {amount} of {stack.Id:X4} into {to}, {stack.ItemCount} left in {from}. " +
                        $"Client ID: {localId:X4}");
        return true;
    }

    public async Task Handle(byte[] frame, double delta)
    {
        var oldSlotId = frame[22] >> 1;
        var newSlotId = frame[21] >> 1;

        var character = clientConnection.GetSelectedCharacter()!;
        SphLogger.Info(
            $"Move to another slot request: from [{Enum.GetName(typeof(BelongingSlot), oldSlotId)}] " +
            $"to [{Enum.GetName(typeof(BelongingSlot), newSlotId)}]");
        var targetSlot = ItemSlotReserve.SlotForWireId(newSlotId) ?? BelongingSlot.Unknown;
        var oldSlot = ItemSlotReserve.SlotForWireId(oldSlotId) ?? BelongingSlot.Unknown;

        var returnToOldSlot = false;

        // The quantity box of a stack drag fills the byte after the cell numbers (1 for a plain move).
        var amount = frame.Length > AmountByte + 1 ? ((frame[AmountByte] | frame[AmountByte + 1] << 8) >> 1) & 0x7FFF : 1;
        if (SplitStack (character, oldSlot, targetSlot, amount))
        {
            return;
        }

        // An unknown target can still be sent back; only a missing source slot is unanswerable
        if (oldSlot is BelongingSlot.Unknown || !character.Items.ContainsKey(oldSlot))
        {
            SphLogger.Warning($"Item not found in slot [{Enum.GetName(oldSlot)}]");
            return;
        }

        var globalOldItemId = character.Items[oldSlot];

        var item = DbConnection.Items.FindById(globalOldItemId);

        if (item is null)
        {
            SphLogger.Warning($"Move: slot [{Enum.GetName(oldSlot)}] points at item {globalOldItemId}, " +
                              $"which is not in the database. Client ID: {localId:X4}");
            return;
        }

        // A bag or book with things in it cannot leave its own slot.
        if (item.HomeSlot == oldSlot && targetSlot != oldSlot && ItemContainers.HasContents(item))
        {
            SphLogger.Info($"Move: [{Enum.GetName(oldSlot)}] holds items and stays put");
            returnToOldSlot = true;
        }

        // Requirements gate wearing, not carrying: anything may sit in a cell.
        if (targetSlot == BelongingSlot.Unknown ||
            (targetSlot != oldSlot && !character.IsItemSlotEmpty (targetSlot)) ||
            !item.IsValidForSlot (targetSlot) ||
            (!ItemDbEntry.IsInventorySlot(targetSlot) && !character.CanUseItem(item)))
        {
            SphLogger.Warning($"Item [{globalOldItemId}] couldn't be used in slot [{Enum.GetName(targetSlot)}]");
            returnToOldSlot = true;

            // Drag does not check requirements, so the refusal uses the client's own wording
            if (character.UnmetRequirement(item) is { } unmet)
            {
                var name = item.Localization.GetValueOrDefault(Locale.Russian, "?");
                clientConnection.MaybeScheduleNetworkPacketSend(
                    MessageEncoder.EncodeToSendFromServer($"Нельзя использовать {name}, {unmet}", "GM",
                        (int)PublicChatType.GM_Outgoing));
            }
        }

        SphLogger.Info($"Item found: {globalOldItemId}");
        if (!returnToOldSlot)
        {
            var lookBefore = CharacterWornLook.Capture(character);
            character.Items.Remove(oldSlot);
            character.Items[targetSlot] = globalOldItemId;
            if (oldSlot == BelongingSlot.Guild || targetSlot == BelongingSlot.Guild)
            {
                character.SyncGuildFromWornEmblem();
                GuildAbilityLoadout.Sync(character, clientConnection.MaybeScheduleNetworkPacketSend);
            }

            if (character.RecalcCurrentStats())
            {
                NetworkedStatsUpdater.Update(character);
            }

            // The worn slot and look exist only in memory until this save
            clientConnection.SaveSelectedCharacter();

            if (lookBefore != CharacterWornLook.Capture(character))
            {
                ActiveClients.Get(localId)?.BroadcastAppearanceRefreshToVisibleClients();
            }
        }

        var moveResult = returnToOldSlot
            ? ItemSlotReserve.Build (localId, oldSlot, item.Id, item.ItemCount)
            : ItemSlotReserve.BuildMove (localId, oldSlot, targetSlot, item.Id, item.ItemCount);
        if (moveResult is not null)
        {
        clientConnection.MaybeScheduleNetworkPacketSend(moveResult);
    }
}}
