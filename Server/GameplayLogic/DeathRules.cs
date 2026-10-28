using System;
using System.Linq;
using SphServer.Helpers;
using SphServer.Shared.Db.DataModels;

namespace SphServer.Server.GameplayLogic;

/// <summary>
///     What retail does when a player dies, measured on the official server (two deaths of one level 31 character):
///     the revive comes 8.3 s after the death hit with 100 hp, in the nearest city (also for a death inside a castle),
///     and about 5.75 percent of title and degree experience is lost.
/// </summary>
public static class DeathRules
{
    // The respawn spot retail used in Torwel for a good-karma character. Other cities use their centre until measured.
    private static readonly WorldCoords TorwelRespawn = new (2301.897, 155.286, -2403.858, -4.311);

    /// <summary>
    ///     Character coordinates are client x and y and a negated z, as the database keeps them. Bad karma uses the
    ///     table of known respawn points of the city when it has one.
    /// </summary>
    public static WorldCoords NearestCityRespawn (CharacterDbEntry character)
    {
        var px = character.X;
        var pz = -character.Z;
        WorldCoords? best = null;
        Continents bestContinent = default;
        Cities bestCity = default;
        var bestDistance = double.MaxValue;

        foreach (var (continent, byType) in SavedCoords.TeleportPoints)
        {
            if (!byType.TryGetValue (PoiType.CityCenter, out var cities))
            {
                continue;
            }

            foreach (var (name, centre) in cities)
            {
                if (!Enum.TryParse<Cities> (name, out var city))
                {
                    continue;
                }

                var dx = px - centre.x;
                var dz = pz - centre.z;
                var distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = centre;
                    bestContinent = continent;
                    bestCity = city;
                }
            }
        }

        if (best is null)
        {
            return new WorldCoords (character.X, -character.Y, pz, character.Angle);
        }

        // The inherited table lists the Arena as the bad-karma spot of Sunpool (a long standing error of the original
        // project), so a point equal to the Arena is not trusted and the city centre is used instead
        if (SavedCoords.RespawnPoints.TryGetValue (bestContinent, out var byCity) &&
            byCity.TryGetValue (bestCity, out var byKarma) && byKarma.TryGetValue (character.Karma, out var known) &&
            !IsArena (known))
        {
            return known;
        }

        return bestCity == Cities.Торвил ? TorwelRespawn : best;
    }

    private static bool IsArena (WorldCoords point) =>
        SavedCoords.TeleportPoints.Values.Any (byType => byType.TryGetValue (PoiType.Other, out var other) &&
                                                         other.TryGetValue ("Арена", out var arena) &&
                                                         Math.Abs (arena.x - point.x) < 1 && Math.Abs (arena.z - point.z) < 1);

    /// <summary>
    ///     Takes the configured percent of the title and degree experience. Returns whether anything changed so the
    ///     caller can push the new values. The experience is progress towards the next level, so it never goes negative.
    /// </summary>
    public static bool ApplyExperienceLoss (CharacterDbEntry character, float percent)
    {
        if (percent <= 0)
        {
            return false;
        }

        var keep = 1.0 - Math.Min(percent, 100f) / 100.0;
        var title = (uint) (character.TitleXP * keep);
        var degree = (uint) (character.DegreeXP * keep);
        if (title == character.TitleXP && degree == character.DegreeXP)
        {
            return false;
        }

        character.TitleXP = title;
        character.DegreeXP = degree;
        return true;
    }
}
