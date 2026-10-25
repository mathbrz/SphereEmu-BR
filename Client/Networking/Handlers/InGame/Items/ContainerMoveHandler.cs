using System;
using System.Linq;
using System.Threading.Tasks;
using SphServer.Client.Networking.GameplayLogic.Stats;
using SphServer.Helpers.Networking;
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
///     Dragging an item into a bag, pouch or book cell, between cells, or from a cell onto a slot.
///     The client changes nothing itself, so every accepted move is answered with the release of
///     the old place, the item re-parented, and the new place filled. A refused move is only logged:
///     the client has not moved anything.
/// </summary>
public class ContainerMoveHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    public Task Handle(byte[] frame, double delta)
    {
        if (!ContainerMoveFrame.TryRead(frame, out var move))
        {
            return Task.CompletedTask;
        }

        var character = clientConnection.GetSelectedCharacter();
        var item = DbConnection.Items.FindById(move.ItemId);
        if (character is null || item is null)
        {
            Refuse($"item {move.ItemId} is unknown");
            return Task.CompletedTask;
        }

        if (move.ToPlayer)
        {
            OutOfContainer(character, item, move.Cell);
        }
        else
        {
            IntoContainer(character, item, move.TargetId, move.Cell);
        }

        return Task.CompletedTask;
    }

    /// <summary>The client's own "add this stack to that one" request (UseItem of 9 bytes naming the destination).</summary>
    public Task HandleStack(byte[] frame)
    {
        if (!StackItemFrame.TryRead(frame, out var request))
        {
            return Task.CompletedTask;
        }

        var character = clientConnection.GetSelectedCharacter();
        var item = DbConnection.Items.FindById(request.ItemId);
        var onto = DbConnection.Items.FindById(request.DestinationId);
        if (character is null || item is null || onto is null || item.Id == onto.Id
            || ItemContainers.CarriedRoot(character, onto) is null
            || (ItemContainers.CarriedRoot(character, item) is null && !IsReachableOnGround(character, item)))
        {
            Refuse($"stack of {request.ItemId:X4} onto {request.DestinationId:X4} is not between carried items");
            return Task.CompletedTask;
        }

        if (!CanStack(onto, item))
        {
            Refuse($"item {item.Id:X4} does not stack onto {onto.Id:X4}");
            return Task.CompletedTask;
        }

        StackOnto(character, onto, item);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Part of a stack dragged into an empty cell of the same bag or book. Retail leaves the original in its cell
    ///     with the rest and answers: the old cell's new count, the load, the item's amount, both cells set, then the
    ///     new item's record (retail capture 2026-10-25, 3 of 5 powders).
    /// </summary>
    public Task HandleSplit(byte[] frame)
    {
        if (!SplitStackFrame.TryRead(frame, out var request))
        {
            return Task.CompletedTask;
        }

        var character = clientConnection.GetSelectedCharacter();
        var container = DbConnection.Items.FindById((int)request.ContainerId);
        if (character is null || container is null || !ItemContainers.IsContainer(container) ||
            ItemContainers.CarriedRoot(character, container) is null)
        {
            Refuse($"split in {request.ContainerId:X4}: not a carried container");
            return Task.CompletedTask;
        }

        var stack = ItemContainers.At(container.Id, request.SourceCell);
        if (stack is null || request.Amount < 1 || request.Amount >= stack.ItemCount ||
            request.DestinationCell < 0 || request.DestinationCell >= ItemContainers.Capacity(container) ||
            ItemContainers.At(container.Id, request.DestinationCell) is not null)
        {
            Refuse($"split of cell {request.SourceCell} into {request.DestinationCell} by {request.Amount} is not possible");
            return Task.CompletedTask;
        }

        var part = ItemDbEntry.Clone(stack);
        part.ItemCount = request.Amount;
        part.ParentContainerId = null;
        part.HeldInItemId = container.Id;
        part.HeldInCell = request.DestinationCell;
        part.OnGround = false;
        DbConnection.Items.Update(part);

        stack.ItemCount -= request.Amount;
        DbConnection.Items.Update(stack);
        clientConnection.SaveSelectedCharacter();

        var type = (ushort)container.WireObjectType;
        Send(CommonPackets.BuildContainerCellCount(container.Id, type, (byte)request.SourceCell,
            ItemContainers.WireWeight(stack)));
        RefreshLoad(character, container);
        Send(CommonPackets.BuildItemAmount((ushort)stack.Id, (ushort)stack.WireObjectType, (ushort)stack.ItemCount));
        Send(CommonPackets.BuildContainerCell(container.Id, type, (byte)request.SourceCell, stack.Id,
            ItemContainers.WireWeight(stack)));
        Send(CommonPackets.BuildContainerCell(container.Id, type, (byte)request.DestinationCell, part.Id,
            ItemContainers.WireWeight(part)));
        Send(ItemRecordEncoder.Encode(part, (ushort)container.Id));
        SphLogger.Info($"Container: split {request.Amount} of {stack.Id:X4} into cell {request.DestinationCell} of " +
                       $"{container.Id:X4}, {stack.ItemCount} left. Client ID: {localId:X4}");
        return Task.CompletedTask;
    }

    private void IntoContainer(CharacterDbEntry character, ItemDbEntry item, int containerId, int cell)
    {
        var container = DbConnection.Items.FindById(containerId);
        if (container is null || !ItemContainers.IsContainer(container) ||
            ItemContainers.CarriedRoot(character, container) is null)
        {
            Refuse($"container {containerId:X4} is not carried");
            return;
        }

        var carried = ItemContainers.CarriedRoot(character, item);
        var fromGround = carried is null && IsReachableOnGround(character, item);
        if (carried is null && !fromGround)
        {
            Refuse($"item {item.Id:X4} is not carried or within reach");
            return;
        }

        if (!ItemContainers.Accepts(container, item) || cell < 0 || cell >= ItemContainers.Capacity(container))
        {
            Refuse($"container {container.Id:X4} does not take item {item.Id:X4} in cell {cell}");
            return;
        }

        if (ItemContainers.At(container.Id, cell) is { } occupant)
        {
            if (occupant.Id != item.Id)
            {
                if (CanStack(occupant, item))
                {
                    StackOnto(character, occupant, item);
                }
                else
                {
                    Refuse($"cell {cell} of {container.Id:X4} is taken");
                }
            }

            return;
        }

        var source = item.HeldInItemId is { } parentId ? DbConnection.Items.FindById(parentId) : null;
        if (source is not null && IsMantraBook(source) && !IsMantraBook(container))
        {
            Refuse("a pasted mantra cannot leave its book");
            return;
        }

        if (source is null && carried is { } home && item.HomeSlot == home.Slot && ItemContainers.HasContents(item))
        {
            Refuse("a container with contents stays in its slot");
            return;
        }

        var oldCell = item.HeldInCell;
        var fromSlot = source is null && carried is { } at ? at.Slot : (BelongingSlot?)null;
        var lookBefore = CharacterWornLook.Capture(character);

        if (fromSlot is not null)
        {
            foreach (var key in character.Items.Where(x => x.Value == item.Id).Select(x => x.Key).ToList())
            {
                character.Items.Remove(key);
            }
        }

        item.HeldInItemId = container.Id;
        item.HeldInCell = cell;
        item.OnGround = false;
        DbConnection.Items.Update(item);
        TakeOffTheGround(item);

        var itemType = (ushort)item.WireObjectType;
        if (source is not null)
        {
            RefreshLoad(character, source);
            Send(CommonPackets.BuildContainerCellRelease(source.Id, (ushort)source.WireObjectType, (byte)oldCell));
        }
        else if (fromSlot is { } leaving && ItemSlotReserve.WireSlotId(leaving) is { } wire)
        {
            Send(CommonPackets.BuildPlayerSlotRelease(localId, (byte)wire));
        }

        if (source?.Id != container.Id)
        {
            Send(CommonPackets.BuildItemContainedTransform(item.Id, itemType));
            Send(CommonPackets.BuildItemSpawnInit(item.Id, itemType, container.Id));
        }

        RefreshLoad(character, container);
        Send(CommonPackets.BuildContainerCell(container.Id, (ushort)container.WireObjectType, (byte)cell, item.Id,
            ItemContainers.WireWeight(item)));

        if (fromSlot is not null && ItemSlotReserve.Build(localId, fromSlot.Value, ItemSlotReserve.NoItem) is { } empty)
        {
            Send(empty);
        }

        AfterWornChange(character, fromSlot, null, lookBefore);
        SphLogger.Info($"Container: item {item.Id:X4} into {container.Id:X4} cell {cell}. Client ID: {localId:X4}");
    }

    private void OutOfContainer(CharacterDbEntry character, ItemDbEntry item, int wireSlot)
    {
        var source = item.HeldInItemId is { } parentId ? DbConnection.Items.FindById(parentId) : null;
        var fromGround = source is null && ItemContainers.CarriedRoot(character, item) is null &&
                         IsReachableOnGround(character, item);
        if (!fromGround && (source is null || ItemContainers.CarriedRoot(character, item) is null))
        {
            Refuse($"item {item.Id:X4} is not in a carried container or within reach");
            return;
        }

        if (ItemSlotReserve.SlotForWireId(wireSlot) is not { } dest || character.Items.ContainsKey(dest))
        {
            Refuse($"slot {wireSlot} is unknown or taken");
            return;
        }

        if (source is not null && IsMantraBook(source))
        {
            Refuse("a pasted mantra cannot leave its book");
            return;
        }

        if (!item.IsValidForSlot(dest) || (!ItemDbEntry.IsInventorySlot(dest) && !character.CanUseItem(item)))
        {
            Refuse($"item {item.Id:X4} cannot go in {Enum.GetName(dest)}");
            return;
        }

        var oldCell = item.HeldInCell;
        var lookBefore = CharacterWornLook.Capture(character);
        item.HeldInItemId = null;
        item.HeldInCell = 0;
        item.OnGround = false;
        DbConnection.Items.Update(item);
        character.PlaceItemInSlot(dest, item.Id);
        TakeOffTheGround(item);

        var itemType = (ushort)item.WireObjectType;
        if (source is not null)
        {
            RefreshLoad(character, source);
            Send(CommonPackets.BuildContainerCellRelease(source.Id, (ushort)source.WireObjectType, (byte)oldCell));
        }

        Send(CommonPackets.BuildItemContainedTransform(item.Id, itemType));
        Send(CommonPackets.BuildItemSpawnInit(item.Id, itemType, ByteSwap(localId)));
        Send(CommonPackets.BuildPlayerCell(localId, (byte)wireSlot, item.Id, ItemContainers.WireWeight(item)));

        AfterWornChange(character, null, dest, lookBefore);
        SphLogger.Info($"Container: item {item.Id:X4} out of {source?.Id.ToString("X4") ?? "the ground"} onto " +
                       $"{Enum.GetName(dest)}. " +
                       $"Client ID: {localId:X4}");
    }

    // Mantras stack in the original: one dropped on an identical one in a cell adds to it.
    private static bool CanStack(ItemDbEntry onto, ItemDbEntry item) =>
        (onto.GameObjectType is GameObjectType.MantraWhite or GameObjectType.MantraBlack
         || ItemRecordEncoder.IsCounted(onto.WireObjectType))
        && onto.GameId == item.GameId && Equals(onto.Suffix, item.Suffix);

    private void StackOnto(CharacterDbEntry character, ItemDbEntry onto, ItemDbEntry item)
    {
        var container = onto.HeldInItemId is { } holder ? DbConnection.Items.FindById(holder) : null;
        var total = onto.ItemCount + item.ItemCount;
        if (total > MaxStack)
        {
            Refuse($"stack of {onto.Id:X4} would hold {total}, more than {MaxStack}");
            return;
        }

        var source = item.HeldInItemId is { } parentId ? DbConnection.Items.FindById(parentId) : null;
        var oldCell = item.HeldInCell;
        var fromSlots = character.Items.Where(x => x.Value == item.Id).Select(x => x.Key).ToList();
        foreach (var slot in fromSlots)
        {
            character.Items.Remove(slot);
        }

        TakeOffTheGround(item);
        onto.ItemCount = total;
        DbConnection.Items.Update(onto);
        DbConnection.Items.Delete(item.Id);
        WorldObjectIndex.Release((ushort)item.Id);
        clientConnection.SaveSelectedCharacter();

        var type = (ushort)onto.WireObjectType;
        if (source is not null)
        {
            RefreshLoad(character, source);
            Send(CommonPackets.BuildContainerCellRelease(source.Id, (ushort)source.WireObjectType, (byte)oldCell));
        }

        foreach (var slot in fromSlots)
        {
            if (ItemSlotReserve.WireSlotId(slot) is { } wire)
            {
                Send(CommonPackets.BuildPlayerSlotRelease(localId, (byte)wire));
            }
        }

        Send(CommonPackets.DespawnEntity((ushort)item.Id));
        // The cell holds the count the book draws; the item holds the one its own tooltip and weight use. The cell
        // update alone left the destination showing its old size.
        Send(CommonPackets.BuildItemAmount((ushort)onto.Id, type, (ushort)total));
        if (container is not null)
        {
            Send(CommonPackets.BuildContainerCellCount(container.Id, (ushort)container.WireObjectType,
                (byte)onto.HeldInCell, ItemContainers.WireWeight(onto)));
            RefreshLoad(character, container);
        }
        else if (character.Items.Where(x => x.Value == onto.Id).Select(x => (BelongingSlot?)x.Key).FirstOrDefault() is
                 { } ontoSlot && ItemSlotReserve.WireSlotId(ontoSlot) is { } ontoWire)
        {
            Send(CommonPackets.BuildPlayerSlotCount(localId, (byte)ontoWire, (ushort)total));
        }

        SphLogger.Info($"Container: stacked {item.Id:X4} onto {onto.Id:X4}, now {total}. Client ID: {localId:X4}");
    }

    private const int MaxStack = ushort.MaxValue;

    private void RefreshLoad(CharacterDbEntry character, ItemDbEntry container)
    {
        foreach (var packet in ContainerLoadEncoder.Build(localId, character, container))
        {
            Send(packet);
        }
    }

    /// <summary>Same follow-through as an equip by drag: guild emblem, stats and appearance, then persist.</summary>
    private void AfterWornChange(CharacterDbEntry character, BelongingSlot? left, BelongingSlot? entered,
        CharacterWornLook.Snapshot lookBefore)
    {
        var touchedGuild = left == BelongingSlot.Guild || entered == BelongingSlot.Guild;
        if (touchedGuild)
        {
            character.SyncGuildFromWornEmblem();
            GuildAbilityLoadout.Sync(character, clientConnection.MaybeScheduleNetworkPacketSend);
        }

        if (character.RecalcCurrentStats())
        {
            NetworkedStatsUpdater.Update(character);
        }

        clientConnection.SaveSelectedCharacter();

        if (lookBefore != CharacterWornLook.Capture(character))
        {
            ActiveClients.Get(localId)?.BroadcastAppearanceRefreshToVisibleClients();
        }
    }

    // A dropped item stays where it fell, so reach is measured to that spot.
    private const double PickupReach = 20;

    private static bool IsReachableOnGround(CharacterDbEntry character, ItemDbEntry item) =>
        (item.OnGround &&
         Math.Sqrt(Math.Pow(item.X - character.X, 2) + Math.Pow(item.Z - character.Z, 2)) <= PickupReach) ||
        (item.DroppedOnGroundBy is not null && GroundItem.Find(item.Id) is { } lying &&
         lying.GlobalPosition.DistanceTo(character.Origin) <= WorldPickupReach);

    // Same reach as upstream's pickup into a character slot, measured to the item lying in the world.
    private const float WorldPickupReach = 10f;

    /// <summary>An item lying in the world (upstream drop) is no longer there once it sits in a cell or slot.</summary>
    private void TakeOffTheGround(ItemDbEntry item)
    {
        if (item.DroppedOnGroundBy is null)
        {
            return;
        }

        item.DroppedOnGroundBy = null;
        DbConnection.Items.Update(item);
        if (ActiveClients.Get(localId) is { } picker)
        {
            GroundItem.Find(item.Id)?.PickedUp(picker);
        }
    }

    private static bool IsMantraBook(ItemDbEntry container) => container.HomeSlot == BelongingSlot.MantraBook;

    private void Send(byte[] packet) => clientConnection.MaybeScheduleNetworkPacketSend(packet);

    private void Refuse(string reason) =>
        SphLogger.Info($"Container move refused: {reason}. Client ID: {localId:X4}");
}
