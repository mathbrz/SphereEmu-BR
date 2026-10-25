using System.Collections.Generic;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Networking;

namespace SphServer.Packets;

/// <summary>
///     What follows a container's own record at login: the cell table, then each held item's record
///     with the container as its parent, depth first. Retail names the cells before the items they
///     point at exist.
/// </summary>
public static class ContainerContentsEncoder
{
    private const int MaxDepth = 6;

    public static IEnumerable<byte[]> Build(ItemDbEntry container, int depth = 0)
    {
        if (depth >= MaxDepth || !ItemContainers.IsContainer(container))
        {
            yield break;
        }

        var held = ItemContainers.Contents(container.Id);
        foreach (var item in held)
        {
            yield return CommonPackets.BuildContainerCell(container.Id, (ushort)container.WireObjectType,
                (byte)item.HeldInCell, item.Id, ItemContainers.WireWeight(item));
        }

        foreach (var item in held)
        {
            yield return ItemRecordEncoder.Encode(item, (ushort)container.Id);
            foreach (var nested in Build(item, depth + 1))
            {
                yield return nested;
            }
        }
    }
}
