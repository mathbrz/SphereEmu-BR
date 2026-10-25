using SphServer.Helpers;
using SphServer.Server.Config;

namespace SphServer.Server.GameplayLogic;

/// <summary>
///     Monsters neither chase nor hit a player standing near a city centre. Players are not affected by it
///     (PvP stays on). The switch starts from <c>CityMonsterProtection</c> in appsettings.json and can be
///     flipped at runtime with /safezone, for events that spawn monsters in a city.
/// </summary>
public static class CityProtection
{
    private static bool? enabled;

    public static bool Enabled
    {
        get => enabled ??= ServerConfig.AppConfig.CityMonsterProtection;
        set => enabled = value;
    }

    /// <summary>Takes world coordinates as the nodes and the database keep them: client x and negated z.</summary>
    public static bool IsProtected(double x, double z)
    {
        if (!Enabled)
        {
            return false;
        }

        var radius = ServerConfig.AppConfig.CityProtectionRadiusMeters;
        foreach (var byType in SavedCoords.TeleportPoints.Values)
        {
            if (!byType.TryGetValue(PoiType.CityCenter, out var cities))
            {
                continue;
            }

            foreach (var city in cities.Values)
            {
                var dx = x - city.x;
                var dz = z + city.z;
                if (dx * dx + dz * dz <= radius * radius)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
