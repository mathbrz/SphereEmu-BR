using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using SphServer.Client.Networking.GameplayLogic.Stats;
using SphServer.Packets;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;
using SphServer.Shared.Networking;
using SphServer.System;
using SphServer.Shared.Networking.DataModel.Serializers;
using static SphServer.Shared.BitStream.SphBitStream;

namespace SphServer.Client.Networking.Handlers.BeforeGame;

public class IngameAckHandler(ushort localId, ClientConnection clientConnection)
    : ISphereClientNetworkingHandler
{
    private static readonly HashSet<Stat> LoginSnapshotStats = [Stat.TitleLevel, Stat.DegreeLevel];

    private SphereTimer? WaitForClientTimer;

    public async Task Handle(byte[] frame, double delta)
    {
        if (WaitForClientTimer is not null)
        {
            WaitForClientTimer.Tick(delta);
            return;
        }

        if (frame.Length != 0x13)
        {
            return;
        }

        var character = clientConnection.GetSelectedCharacter();

        if (character is null)
        {
            // should never happen
            SphLogger.Error($"SRV {localId:X4}: Selected character is null");
            return;
        }

        var missing = character.Items
            .Where(x => DbConnection.Items.FindById(x.Value) is null)
            .Select(x => x.Key)
            .ToList();

        var shouldSave = false;

        if (missing.Count > 0)
        {
            foreach (var slot in missing)
            {
                character.Items.Remove(slot);
            }
            shouldSave = true;
            SphLogger.Warning($"SRV {localId:X4}: Cleared {missing.Count} slot(s) whose item is gone: " +
                              $"{string.Join(", ", missing.Select(x => Enum.GetName(x)))}");
        }

        character.SyncGuildFromWornEmblem();

        if (GuildAbilityLoadout.Sync(character, send: null))
        {
            shouldSave = true;
        }

        if (shouldSave)
        {
            clientConnection.SaveSelectedCharacter();
        }

        character.RecalcCurrentStats();

        SphLogger.Info($"SRV {localId:X4}: Declaring {character.Items.Count} carried item(s), " +
                       $"money {character.Money}");

        foreach (var packet in CharacterItemDeclaration.Build(localId, character))
        {
            clientConnection.MaybeScheduleNetworkPacketSend(packet);
        }

        clientConnection.MaybeScheduleNetworkPacketSend (ItemSlotReserve.BuildUnlock (localId));

        // After items: GuildPlus64/rank and worn bonuses need the emblem process to exist first.
        // The world snapshot sets title/degree (i37/i38) itself; resending them here made the client
        // announce "You have received the title" on every login. The snapshot only carries them while the
        // packed value stays under 4096 (no rebirth); beyond that the client drops the level and the
        // fields after it, so the stat send has to supply the levels.
        // With a guild the client was seen to drop the levels from the snapshot (sheet showed 1/1), so they
        // are only left out for guildless characters.
        var snapshotCarriesLevels = character.Guild == Guild.None &&
                                    character.TitleMinusOne < 60 && character.DegreeMinusOne < 60 &&
                                    character.DegreeMinusOne * 100 + character.TitleMinusOne < 4096;
        NetworkedStatsUpdater.Update(character, clientConnection.MaybeScheduleNetworkPacketSend, full: true,
            skipStats: snapshotCarriesLevels ? LoginSnapshotStats : null);

        WaitForClientTimer = new(0.05f, false, clientConnection.MoveToNextBeforeGameStage);
    }
}
