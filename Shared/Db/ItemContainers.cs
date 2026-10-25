using System;
using System.Collections.Generic;
using System.Linq;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.WorldState;
using SphServer.Helpers;

namespace SphServer.Shared.Db;

/// <summary>
///     Items held inside other items (bags, pouches, mantra books). The cell table lives on the held
///     item (<see cref="ItemDbEntry.HeldInItemId"/> and <see cref="ItemDbEntry.HeldInCell"/>), so a
///     container's contents are whatever points at it.
/// </summary>
public static class ItemContainers
{
    private const int DefaultCapacity = 32;

    /// <summary>Guard against a corrupt parent chain.</summary>
    private const int MaxDepth = 8;

    // The client draws and limits the real window; only the pouch's size is known (4 cells).
    private static readonly Dictionary<ObjectType, int> Capacities = new()
    {
        [ObjectType.Sack] = 4
    };

    public static bool IsContainer(ItemDbEntry item) =>
        PacketPartMapping.ItemBagObjectTypes.Contains(item.WireObjectType) ||
        PacketPartMapping.ItemRecipeBagObjectTypes.Contains(item.WireObjectType);

    public static int Capacity(ItemDbEntry container) =>
        Capacities.GetValueOrDefault(container.WireObjectType, DefaultCapacity);

    public static List<ItemDbEntry> Contents(int containerId) =>
        DbConnection.Items.Find(x => x.HeldInItemId == containerId).OrderBy(x => x.HeldInCell).ToList();

    public static bool HasContents(ItemDbEntry container) =>
        DbConnection.Items.Exists(x => x.HeldInItemId == container.Id);

    public static ItemDbEntry? At(int containerId, int cell) =>
        DbConnection.Items.FindOne(x => x.HeldInItemId == containerId && x.HeldInCell == cell);

    public static int? FirstFreeCell(ItemDbEntry container)
    {
        var taken = Contents(container.Id).Select(x => x.HeldInCell).ToHashSet();
        for (var cell = 0; cell < Capacity(container); cell++)
        {
            if (!taken.Contains(cell))
            {
                return cell;
            }
        }

        return null;
    }

    /// <summary>Weight of the stack plus everything inside it, in grams.</summary>
    public static long TotalWeight(ItemDbEntry item, int depth = 0)
    {
        var total = (long)item.Weight * Math.Max(1, item.ItemCount);
        if (depth >= MaxDepth || !IsContainer(item))
        {
            return total;
        }

        return total + Contents(item.Id).Sum(x => TotalWeight(x, depth + 1));
    }

    public static uint WireWeight(ItemDbEntry item) => (uint)Math.Clamp(TotalWeight(item), 0, uint.MaxValue);

    /// <summary>Whether the container may hold the item: books take their own kind, nothing holds itself.</summary>
    public static bool Accepts(ItemDbEntry container, ItemDbEntry item)
    {
        if (container.Id == item.Id || IsInside(container, item))
        {
            return false;
        }

        // A mantra book only takes mantras, a map book maps, a recipe book scrolls.
        return container.HomeSlot is { } home && !ItemDbEntry.IsInventorySlot(home) && home != BelongingSlot.Backpack
            ? FitsInBook(home, item)
            : true;
    }

    // What goes inside a book is not what goes in its character slot: the slot takes the book itself, the book takes
    // the mantras, maps or scrolls.
    private static bool FitsInBook(BelongingSlot bookSlot, ItemDbEntry item) => bookSlot switch
    {
        BelongingSlot.MantraBook => item.GameObjectType is GameObjectType.MantraWhite or GameObjectType.MantraBlack,
        BelongingSlot.MapBook => item.GameObjectType is GameObjectType.Map,
        BelongingSlot.RecipeBook => item.GameObjectType is GameObjectType.Scroll,
        _ => item.IsValidForSlot(bookSlot)
    };

    /// <summary>True when <paramref name="item"/> sits somewhere beneath <paramref name="ancestor"/>.</summary>
    public static bool IsInside(ItemDbEntry item, ItemDbEntry ancestor)
    {
        var current = item;
        for (var depth = 0; depth < MaxDepth && current.HeldInItemId is { } parentId; depth++)
        {
            if (parentId == ancestor.Id)
            {
                return true;
            }

            current = DbConnection.Items.FindById(parentId);
            if (current is null)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>The character slot and item at the top of the chain that holds <paramref name="item"/>, or null when it is not carried.</summary>
    public static (BelongingSlot Slot, ItemDbEntry Root)? CarriedRoot(CharacterDbEntry character, ItemDbEntry item)
    {
        var current = item;
        for (var depth = 0; depth <= MaxDepth && current is not null; depth++)
        {
            var held = character.Items.Where(x => x.Value == current.Id && x.Key != BelongingSlot.MainHand)
                .Select(x => (BelongingSlot?)x.Key).FirstOrDefault();
            if (held is { } slot)
            {
                return (slot, current);
            }

            current = current.HeldInItemId is { } parentId ? DbConnection.Items.FindById(parentId) : null;
        }

        return null;
    }

    /// <summary>Every item held beneath the character's slots, container contents included.</summary>
    public static IEnumerable<ItemDbEntry> Descendants(ItemDbEntry container, int depth = 0)
    {
        if (depth >= MaxDepth)
        {
            yield break;
        }

        foreach (var child in Contents(container.Id))
        {
            yield return child;
            foreach (var deeper in Descendants(child, depth + 1))
            {
                yield return deeper;
            }
        }
    }
}
