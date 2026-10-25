using System.Globalization;
using SphereHelpers.Extensions;
using SphServer.Client;
using SphServer.Client.Networking.GameplayLogic.Stats;
using SphServer.Helpers;
using SphServer.Packets;
using SphServer.Server.Config;
using SphServer.Server.GameplayLogic;
using SphServer.Server.UI.Admin;
using SphServer.Shared.BitStream;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.GameData.Enums;
using SphServer.Shared.Logger;
using SphServer.Shared.Networking;
using SphServer.Shared.Networking.Chat.Encoders;
using SphServer.Shared.Networking.DataModel.Serializers;
using SphServer.Shared.WorldState;
using SphServer.System;
using static SphServer.Shared.Networking.DataModel.Serializers.SphereDbEntrySerializerBase;

namespace SphServer.Server.Debug.Parser;

public enum ConsoleCommandParseResult
{
    OK,
    ERROR
}

public partial class ConsoleCommandParser
{
    /// <summary>
    /// How many values /stats reads, one per indexed assignment below.
    /// </summary>
    private const int StatsArgumentCount = 26;


    private static readonly Dictionary<ushort, ConsoleCommandParser> ParserCache = new();
    private CharacterDbEntry currentCharacterDbEntry;
    private readonly Dictionary<string, Action<string>> RegisteredCommands = new();

    // Reconnect reuses ClientIndex but allocates a new SphereClient; a cached reference sends into
    // the freed connection
    private SphereClient? sphereClient => ActiveClients.Get(currentCharacterDbEntry.ClientIndex);

    private ConsoleCommandParser(CharacterDbEntry characterDbEntry)
    {
        currentCharacterDbEntry = characterDbEntry;
    }

    public static ConsoleCommandParser Get(CharacterDbEntry characterDbEntry)
    {
        if (!ParserCache.TryGetValue(characterDbEntry.ClientIndex, out var parser))
        {
            parser = new ConsoleCommandParser(characterDbEntry);
            parser.InitCommands();
            ParserCache[characterDbEntry.ClientIndex] = parser;
        }
        else
        {
            // The character row is a new instance after reconnect or reselect
            parser.currentCharacterDbEntry = characterDbEntry;
        }

        return parser;
    }

    public static void Invalidate(ushort clientIndex) => ParserCache.Remove(clientIndex);

    private void InitCommands()
    {
        RegisteredCommands["stats"] = UpdateStats;
        RegisteredCommands["money"] = UpdateMoney;
        // The game client never forwards "/money" (swallowed locally), so expose the same command as /gold.
        RegisteredCommands["gold"] = UpdateMoney;
        RegisteredCommands["lvl"] = SetLevel;
        RegisteredCommands["guild"] = SetGuild;
        RegisteredCommands["attr"] = SetAttributes;
        RegisteredCommands["inv"] = ListInventory;
        RegisteredCommands["karma"] = SetKarma;
        RegisteredCommands["die"] = Die;
        RegisteredCommands["msg"] = SendMessage;
        RegisteredCommands["clan"] = UpdateClan;
        RegisteredCommands["packethex"] = SendPacketHex;
        RegisteredCommands["packet"] = SendPacket;
        RegisteredCommands["buff"] = Buff;
        // /clientmob is a client-only template; /mob is a server node
        RegisteredCommands["clientmob"] = Mob;
        RegisteredCommands["mob"] = SpawnRealMonster;
        RegisteredCommands["mobid"] = MobById;
        RegisteredCommands["loot"] = Loot;
        RegisteredCommands["give"] = Give;
        RegisteredCommands["giveinv"] = GiveToInventory;
        RegisteredCommands["giveinvns"] = GiveToInventoryByNameWithSuffix;
        RegisteredCommands["bag"] = GiveContainer;
        RegisteredCommands["clearinv"] = ClearInventory;
        RegisteredCommands["tp"] = Teleport;
        RegisteredCommands["pos"] = ShowPosition;
        RegisteredCommands["safezone"] = ToggleCityProtection;
        RegisteredCommands["ghost"] = ToggleGhost;
        RegisteredCommands["time"] = SetGameTime;
    }

    // A GM chat line when a client is attached, otherwise the server console
    private void SendFeedback(string text)
    {
        // A failed guard is otherwise invisible server-side
        SphLogger.Info($"GM feedback: {text}");

        if (sphereClient is null)
        {
            Console.WriteLine(text);
            return;
        }

        var response = MessageEncoder.EncodeToSendFromServer("GM: " + text, "GM",
            (int)PublicChatType.GM_Outgoing);
        sphereClient.MaybeQueueNetworkPacketSend(response);
    }

    public bool IsRegistered(string? input)
    {
        return TrySplitCommand(input, out var command, out _) && RegisteredCommands.ContainsKey(command);
    }

    public ConsoleCommandParseResult Parse(string? input)
    {
        if (!TrySplitCommand(input, out var command, out var args) ||
            !RegisteredCommands.TryGetValue(command, out var value))
        {
            return ConsoleCommandParseResult.ERROR;
        }

        try
        {
            value(args);
        }
        catch (Exception ex)
        {
            SendFeedback($"/{command} failed: {ex.Message}");
            SphLogger.Error($"GM command /{command} threw", ex);
        }

        return ConsoleCommandParseResult.OK;
    }

    private static bool TrySplitCommand(string? input, out string command, out string args)
    {
        command = string.Empty;
        args = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
        {
            return false;
        }

        var split = input[1..].Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
        {
            return false;
        }

        command = split[0].ToLowerInvariant();
        args = split.Length > 1 ? split[1] : string.Empty;
        return true;
    }

    private void UpdateStats(string args)
    {
        var stats = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (stats.Length < StatsArgumentCount)
        {
            SendFeedback($"Usage: /stats takes {StatsArgumentCount} values, got {stats.Length}.");
            return;
        }

        // Max HP/MP, PD/MD and PA/MA (0, 1, 12, 13, 24, 25) come from levels and worn items in
        // RecalcCurrentStats, so those slots are read past
        currentCharacterDbEntry.CurrentSatiety = ushort.Parse(stats[2]);
        currentCharacterDbEntry.MaxSatiety = ushort.Parse(stats[3]);
        // Recalc rebuilds current stats from base, so writing current ones would not stick
        currentCharacterDbEntry.BaseStrength = int.Parse (stats[4]);
        currentCharacterDbEntry.BaseAgility = int.Parse (stats[5]);
        currentCharacterDbEntry.BaseAccuracy = int.Parse (stats[6]);
        currentCharacterDbEntry.BaseEndurance = int.Parse (stats[7]);
        currentCharacterDbEntry.BaseEarth = int.Parse (stats[8]);
        currentCharacterDbEntry.BaseAir = int.Parse (stats[9]);
        currentCharacterDbEntry.BaseWater = int.Parse (stats[10]);
        currentCharacterDbEntry.BaseFire = int.Parse (stats[11]);
        currentCharacterDbEntry.TitleMinusOne = ushort.Parse(stats[14]);
        currentCharacterDbEntry.DegreeMinusOne = ushort.Parse(stats[15]);
        currentCharacterDbEntry.Karma = (KarmaTypes)ushort.Parse(stats[16]);
        currentCharacterDbEntry.KarmaCount = ushort.Parse(stats[17]);
        currentCharacterDbEntry.SyncKarmaFromCount();
        currentCharacterDbEntry.TitleXP = uint.Parse(stats[18]);
        currentCharacterDbEntry.DegreeXP = uint.Parse(stats[19]);
        currentCharacterDbEntry.AvailableTitleStats = ushort.Parse(stats[20]);
        currentCharacterDbEntry.AvailableDegreeStats = ushort.Parse(stats[21]);
        currentCharacterDbEntry.ClanRank = (ClanRank)ushort.Parse(stats[22]);
        currentCharacterDbEntry.Money = int.Parse(stats[23]);
        currentCharacterDbEntry.RecalcCurrentStats ();
        NetworkedStatsUpdater.Update (currentCharacterDbEntry, full: true);
        sphereClient?.SendMoney ();
        sphereClient?.SaveCharacter ();
        SendFeedback("Stats updated.");
    }

    private void UpdateMoney(string args)
    {
        var stats = args.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (stats.Length < 1 || !int.TryParse(stats[0], out var money))
        {
            SendFeedback("Usage: /gold <amount>");
            return;
        }

        currentCharacterDbEntry.Money = money;
        sphereClient?.SendMoney ();
        sphereClient?.SaveCharacter();
        SendFeedback($"Money set to {money}.");
    }

    // Chat paste is unsupported by the client, so /stats (26 values) is impractical to type.
    private void SetLevel(string args)
    {
        var values = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var character = currentCharacterDbEntry;
        var oldTitle = character.TitleMinusOne;
        var oldDegree = character.DegreeMinusOne;
        int titleRebirth = oldTitle / CharacterDataHelper.LevelsPerCycle;
        int degreeRebirth = oldDegree / CharacterDataHelper.LevelsPerCycle;
        if (values.Length < 2
            || !int.TryParse(values[0], out var title) || !int.TryParse(values[1], out var degree)
            || (values.Length > 2 && !int.TryParse(values[2], out titleRebirth))
            || (values.Length > 3 && !int.TryParse(values[3], out degreeRebirth)))
        {
            SendFeedback($"Usage: /lvl <title 1-{CharacterDataHelper.LevelsPerCycle}> " +
                         $"<degree 1-{CharacterDataHelper.LevelsPerCycle}> " +
                         $"[title rebirths 0-{CharacterDataHelper.MaxRebirths}] [degree rebirths]");
            return;
        }

        // Levels cap at 60 per cycle; a rebirth restarts the track at 1 in the next cycle.
        title = Math.Clamp(title, 1, CharacterDataHelper.LevelsPerCycle);
        degree = Math.Clamp(degree, 1, CharacterDataHelper.LevelsPerCycle);
        titleRebirth = Math.Clamp(titleRebirth, 0, CharacterDataHelper.MaxRebirths);
        degreeRebirth = Math.Clamp(degreeRebirth, 0, CharacterDataHelper.MaxRebirths);
        var newTitle = titleRebirth * CharacterDataHelper.LevelsPerCycle + title - 1;
        var newDegree = degreeRebirth * CharacterDataHelper.LevelsPerCycle + degree - 1;
        // Same sequence as the admin panel's level edit: LevelUp rebuilds points and max HP/MP.
        if (!character.LevelUp(newTitle, newDegree))
        {
            SendFeedback("Title and degree already at those levels.");
            return;
        }

        if (character.TitleMinusOne != oldTitle)
        {
            character.TitleXP = 0;
        }

        if (character.DegreeMinusOne != oldDegree)
        {
            character.DegreeXP = 0;
        }

        NetworkedStatsUpdater.Update(character, full: true);
        sphereClient?.SaveCharacter();
        SendFeedback(
            $"Title {character.TitleMinusOne % CharacterDataHelper.LevelsPerCycle + 1} " +
            $"(rebirths {character.TitleMinusOne / CharacterDataHelper.LevelsPerCycle}), " +
            $"degree {character.DegreeMinusOne % CharacterDataHelper.LevelsPerCycle + 1} " +
            $"(rebirths {character.DegreeMinusOne / CharacterDataHelper.LevelsPerCycle}), " +
            $"points {character.AvailableTitleStats}/{character.AvailableDegreeStats}.");
    }

    // Takes the character to 0 HP through the normal health-change path (death wire, respawn timer), with no
    // karma penalty, to exercise the respawn without a hostile mob or a self-targeted hit.
    // /time [day|night|HH[:MM]]: sets the world clock everyone sees (day = 12:00, night = 00:00) and resends it
    // at once. With no argument it only reports the time. Real time still runs afterwards, at 12x.
    private void SetGameTime(string args)
    {
        if (!ServerConfig.AppConfig.GameClockSync)
        {
            SendFeedback("/time is off: GameClockSync is false in appsettings.json (the client ignored the clock, see HANDOFF).");
            return;
        }

        var value = args.Trim().ToLowerInvariant();
        if (value.Length > 0)
        {
            int hour, minute = 0;
            switch (value)
            {
                case "day":
                    hour = 12;
                    break;
                case "night":
                    hour = 0;
                    break;
                default:
                    var parts = value.Split(':');
                    if (!int.TryParse(parts[0], out hour) || hour is < 0 or > 23 ||
                        (parts.Length > 1 && (!int.TryParse(parts[1], out minute) || minute is < 0 or > 59)))
                    {
                        SendFeedback("Usage: /time [day|night|HH[:MM]]");
                        return;
                    }

                    break;
            }

            GameClock.SetTimeOfDay(hour, minute);
            foreach (var client in ActiveClients.GetAll().Values)
            {
                if (client.IsWorldReady)
                {
                    client.MaybeQueueNetworkPacketSend(CommonPackets.BuildClientSky(GameClock.Counter));
                }
            }
        }

        SendFeedback($"Game time is {GameClock.TimeOfDay}.");
    }

    // Client coordinates (x, y, z), the ones /tp takes; the database keeps Y and Z negated.
    private void ShowPosition(string args)
    {
        var character = currentCharacterDbEntry;
        SendFeedback($"x={character.X:F1} y={-character.Y:F1} z={-character.Z:F1} angle={character.Angle:F2}");
    }

    // /safezone [on|off]: monsters leave players near a city centre alone; no argument flips it.
    private void ToggleCityProtection(string args)
    {
        var value = args.Trim().ToLowerInvariant();
        CityProtection.Enabled = value switch
        {
            "on" => true,
            "off" => false,
            _ => !CityProtection.Enabled
        };
        SendFeedback($"City protection against monsters is {(CityProtection.Enabled ? "ON" : "OFF")}.");
    }

    // /ghost [on|off]: monsters neither chase nor hit this GM. Lasts until the client disconnects.
    private void ToggleGhost(string args)
    {
        if (sphereClient is null)
        {
            SendFeedback("/ghost needs a connected client.");
            return;
        }

        var value = args.Trim().ToLowerInvariant();
        sphereClient.IgnoredByMonsters = value switch
        {
            "on" => true,
            "off" => false,
            _ => !sphereClient.IgnoredByMonsters
        };
        SendFeedback($"Monsters {(sphereClient.IgnoredByMonsters ? "now ignore" : "notice")} you.");
    }

    private void Die(string args)
    {
        var character = currentCharacterDbEntry;
        if (sphereClient is null || character.CurrentHP <= 0)
        {
            SendFeedback("Nothing to do: already dead or not connected.");
            return;
        }

        sphereClient.EnqueueClientEvent(new CharacterHealthChangeEvent(character.ClientIndex, -character.CurrentHP));
    }

    // Karma count runs from -5000 (bad) to 5000 (good), the same clamp kill karma uses; the tier follows it.
    private void SetKarma(string args)
    {
        const int limit = 5000;
        var count = 0;
        var values = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length > 1 || (values.Length == 1 && !int.TryParse(values[0], out count)))
        {
            SendFeedback($"Usage: /karma [value {-limit} to {limit}] (default 0 = neutral)");
            return;
        }

        var character = currentCharacterDbEntry;
        character.KarmaCount = Math.Clamp(count, -limit, limit);
        character.SyncKarmaFromCount();
        NetworkedStatsUpdater.Update(character, full: true);
        sphereClient?.SaveCharacter();
        SendFeedback($"Karma set to {character.KarmaCount} ({character.Karma}).");
    }

    // What the server believes the character carries, one chat line per item (slot, game id, name).
    private void ListInventory(string args)
    {
        var character = currentCharacterDbEntry;
        if (character.Items.Count == 0)
        {
            SendFeedback("Inventory is empty.");
            return;
        }

        SendFeedback($"{character.Items.Count} item(s) held by the server:");
        foreach (var (slot, itemId) in character.Items.OrderBy(x => x.Key))
        {
            var item = DbConnection.Items.FindById(itemId);
            var name = item is null
                ? "(missing from database)"
                : item.Localization.GetValueOrDefault(Locale.English,
                    item.Localization.GetValueOrDefault(Locale.Russian, "?"));
            var suffix = item is null || item.Suffix == ItemSuffix.None ? "" : $" [{item.Suffix}]";
            SendFeedback($"{slot}: id {item?.GameId.ToString() ?? "?"} item {itemId} {name}{suffix}");
        }
    }

    // Equip checks read the current attributes, which RecalcCurrentStats rebuilds from the base ones, so
    // /stats (current values) would not hold; set the base attributes instead and leave the point pools alone.
    private void SetAttributes(string args)
    {
        const int maxAttribute = 9999;
        var values = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new int[values.Length];
        if (values.Length is not (1 or 8) || values.Where((v, i) => !int.TryParse(v, out parsed[i])).Any())
        {
            SendFeedback("Usage: /attr <value for all 8> or /attr <str> <agi> <acc> <end> <earth> <air> <water> <fire>");
            return;
        }

        int[] all = values.Length == 1 ? Enumerable.Repeat(parsed[0], 8).ToArray() : parsed;
        all = all.Select(v => Math.Clamp(v, 0, maxAttribute)).ToArray();

        var character = currentCharacterDbEntry;
        character.BaseStrength = all[0];
        character.BaseAgility = all[1];
        character.BaseAccuracy = all[2];
        character.BaseEndurance = all[3];
        character.BaseEarth = all[4];
        character.BaseAir = all[5];
        character.BaseWater = all[6];
        character.BaseFire = all[7];
        character.RecalcCurrentStats();
        NetworkedStatsUpdater.Update(character, full: true);
        sphereClient?.SaveCharacter();
        SendFeedback($"Attributes set: STR {all[0]} AGI {all[1]} ACC {all[2]} END {all[3]} " +
                     $"EAR {all[4]} AIR {all[5]} WAT {all[6]} FIR {all[7]}.");
    }

    // Membership is the emblem in the guild slot, so this goes through the admin panel action (equips the
    // emblem, syncs abilities, pushes stats). Without it a plain Guild field is reset on the next login.
    private void SetGuild(string args)
    {
        var values = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var guilds = GuildCatalog.LetterOrder;
        if (values.Length == 0)
        {
            SendFeedback("Usage: /guild <name or number> [rank 1-6] | /guild none. " +
                         string.Join(", ", guilds.Select((g, i) => $"{i + 1}={g}")));
            return;
        }

        Guild guild;
        if (values[0].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            guild = Guild.None;
        }
        else if (int.TryParse(values[0], out var number) && number >= 1 && number <= guilds.Length)
        {
            guild = guilds[number - 1];
        }
        else
        {
            var matches = guilds.Where(g => g.ToString().StartsWith(values[0], StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                SendFeedback($"Unknown or ambiguous guild '{values[0]}'. Use /guild to list them.");
                return;
            }

            guild = matches[0];
        }

        var rank = 1;
        if (values.Length > 1 && !int.TryParse(values[1], out rank))
        {
            SendFeedback("Rank must be a number from 1 to 6.");
            return;
        }

        rank = Math.Clamp(rank, 1, (int)GuildRank.Expert + 1);
        if (!AdminClientActions.SetGuild(currentCharacterDbEntry.ClientIndex, guild, rank - 1))
        {
            SendFeedback($"Could not set guild {guild}.");
            return;
        }

        SendFeedback(guild == Guild.None
            ? "Guild removed."
            : $"Guild {guild}, rank {rank} ({(GuildRank)(rank - 1)}).");
    }

    private void SendMessage(string args)
    {
        var chatData = args.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (chatData.Length < 3)
        {
            SendFeedback("Usage: /msg <chat_type> <name> <message>");
            return;
        }

        var chatType = int.Parse(chatData[0]);
        Console.WriteLine(chatType);

        var name = chatData[1].Replace("_", " ");
        var message = string.Join(" ", chatData[2..]);

        message = name + ": " + message;
        // <l="player://Обычный мул\[br\]\[img=\"sep,mid,0,4,0,2\"\]\[br\]\[t=\"#UISTR_TT_IW32a\"\]\[img=\"inf_32,mid,0,2,6,2\"\] \[cl=EEEEEE\]странник (2)\[cl=EEEEEE\]\[/t\]\[br\]\[t=\"#UISTR_TT_IW33a\"\]\[img=\"inf_33,mid,0,2,6,2\"\] \[cl=EEEEEE\]неучёный (1) \[cl=EEEEEE\]\[/t\]\[br\]Клан разный шмот (Сеньор)\[br\]\[img=\"sep,mid,0,4,0,2\"\]">Обычный мул</l>: abc 
        var response = MessageEncoder.EncodeToSendFromServer(message, name, chatType);

        sphereClient?.MaybeQueueNetworkPacketSend(response);
    }

    private void UpdateClan(string args)
    {
        var chatData = args.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (chatData.Length < 2 || !int.TryParse(chatData[1], out var targetRank))
        {
            SendFeedback("Usage: /clan <action> <value>");
            return;
        }

        var action = chatData[0].ToLowerInvariant();
        switch (action)
        {
            case "rank":
                var clan = currentCharacterDbEntry.Clan;
                if (clan is null)
                {
                    Console.WriteLine("No clan");
                    return;
                }

                currentCharacterDbEntry.ClanRank = (ClanRank)targetRank;
                NetworkedStatsUpdater.Update(currentCharacterDbEntry);
                sphereClient?.BroadcastClanRefreshToVisibleClients();
                break;
        }
    }

    public static byte[] BuildClanRankPacket(ushort clientIndex, string clanName, int targetRank) =>
        CommonPackets.BuildClanRankPacket(clientIndex, clanName, targetRank);

    private void SendPacketHex(string args)
    {
        var chatData = args.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (chatData.Length < 1)
        {
            Console.WriteLine("usage: /sendpackethex packet");
        }
        else
        {
            try
            {
                var content = Convert.FromHexString(chatData[1]);
                sphereClient?.MaybeQueueNetworkPacketSend(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Not a hex string: " + ex.Message);
            }
        }
    }

    private void SendPacket(string args)
    {
        SphLogger.Info($"Sending debug command: /packet {args}");
        DebugConsole.SendSpherePacket($"/packet {args}", bytes => sphereClient?.MaybeQueueNetworkPacketSend(bytes));
    }

    private void Buff(string args)
    {
        var jumpx4 =
            "3F002C01006AF6B98878800F80842E090000000000000000409145068002C0400903C0010000000000000000000044EDF9994D83C00A0F07F70391E1004F6F";
        // 	
        //	 03C0120080DE7E0D8307F80048E8920000000000000000001459640028000C9430001C0000000000000000000040D49E9FD93408ACF070703F10F90D50C200
        var runSpeed =
            "3F002C0100720A2EC278800F80842E0900000000000000004091450680020C3CBD011C0000000000000000000040D49ECFE13408A8F00704046C28004F6F00";
        //   3F002C01002CEF8F9578800F80842E090000000000000000409145068002C0400903C0010000000000000000000044EDF91C4E83800A0F0704046C2800250C
        // var test =
        // 	"3F002C010012DF127E78800F80842E090000000000000000409145068002C0C0DB13C0010000000000000000000044ED799B4D83000A0F07E80304AF044F6F";
        sphereClient?.MaybeQueueNetworkPacketSend(Convert.FromHexString(jumpx4));
        sphereClient?.MaybeQueueNetworkPacketSend(Convert.FromHexString(runSpeed));
        // StreamPeer.PutData(Convert.FromHexString(test));
    }

    private void Mob(string args)
    {
        var split = args.Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mobPacketName = split.Length == 0
            ? "mob"
            : "mob_" + split[0];
        DebugConsole.SendSpherePacket($"/packet {mobPacketName} onme",
            bytes => sphereClient?.MaybeQueueNetworkPacketSend(bytes));
    }

    private void MobById(string args)
    {
        var split = args.Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 1 || !int.TryParse(split[0], out var mobId))
        {
            Console.WriteLine(
                "Usage: /spawn_mob_id <id>. For the list of IDs check entityNamesCollected file");
        }
        else
        {
            DebugConsole.SendSpherePacket("/packet mob_assassin onme",
                bytes => sphereClient?.MaybeQueueNetworkPacketSend(bytes), true,
                list =>
                {
                    foreach (var idPart in list.Where(x => x.Name == "mob_type"))
                    {
                        var bits = BitStreamExtensions.IntToBits(mobId, 16).ToList();
                        idPart.Value = bits;
                    }
                });
        }
    }

    private void Loot(string args)
    {
        ItemContainerDbEntry.CreateHierarchyWithContents(currentCharacterDbEntry.X, currentCharacterDbEntry.Y,
            currentCharacterDbEntry.Z + 1,
            1,
            LootRatity.DEFAULT_MOB);
        ItemContainerDbEntry.CreateHierarchyWithContents(currentCharacterDbEntry.X, currentCharacterDbEntry.Y,
            currentCharacterDbEntry.Z + 2,
            1,
            LootRatity.DEFAULT_MOB);
        ItemContainerDbEntry.CreateHierarchyWithContents(currentCharacterDbEntry.X, currentCharacterDbEntry.Y,
            currentCharacterDbEntry.Z + 3,
            1,
            LootRatity.DEFAULT_MOB);
    }

    private void Teleport(string args)
    {
        var split = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length > 1)
        {
            try
            {
                var coords = split
                    .Select (x => double.Parse (x.Replace (',', '.'), CultureInfo.InvariantCulture))
                    .ToArray();
                sphereClient?.MaybeQueueNetworkPacketSend(
                    new CharacterDbEntrySerializer(currentCharacterDbEntry).GetTeleportByteArray(new WorldCoords(
                        coords[0], coords[1], coords[2])));
                sphereClient?.GrantRespawnProtection ();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        else
        {
            try
            {
                // continent:type:point
                var location = split[0].Split(':');
                if (Enum.TryParse<Continents>(location[0], out var continent))
                {
                    if (Enum.TryParse<PoiType>(location[1], out var poiType))
                    {
                        var coords = SavedCoords.TeleportPoints[continent][poiType][location[2]];
                        sphereClient?.MaybeQueueNetworkPacketSend(
                            new CharacterDbEntrySerializer(currentCharacterDbEntry).GetTeleportByteArray(coords));
                        sphereClient?.GrantRespawnProtection ();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
