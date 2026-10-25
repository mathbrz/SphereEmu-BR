using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using SphServer.Server.Config;
using SphServer.Shared.Db;
using SphServer.Shared.Db.DataModels;
using SphServer.Shared.Logger;

namespace SphServer.Server.Login.Auth;

public static class LoginManager
{
    // Hashes written before the iteration count was stored: base64(salt16 + pbkdf2-sha256-100(20)).
    private const int LegacyIterations = 100;
    private const int LegacyHashBytes = 20;
    private const int Iterations = 600_000;
    private const int HashBytes = 32;
    private const int SaltBytes = 16;
    private const string CurrentPrefix = "v2:";

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, (int Count, DateTime FirstUtc)> failures =
        new(StringComparer.OrdinalIgnoreCase);

    public static PlayerDbEntry? CheckLoginAndGetPlayer (string login, string password,
        ushort playerIndex, bool createOnNewLogin = true)
    {
        var playerCollection = DbConnection.Players;

        var player = playerCollection.Query()
            .Include(["$.Characters[*]", "$.Characters[*].Clan"])
            .Where(x => x.Login == login).FirstOrDefault();

        if (player is not null)
        {
            SphLogger.Info($"SRV: Login [{login}] fetched");

            if (IsLockedOut(login))
            {
                SphLogger.Error($"SRV: Too many wrong passwords for [{login}], try again later");
                return null;
            }

            if (!EqualsHashed(password, player.PasswordHash))
            {
                RegisterFailure(login);
                SphLogger.Error($"SRV: Wrong password for [{login}]");
                return null;
            }

            ClearFailures(login);
            SphLogger.Info($"SRV: Existing player [{player.Id}] found for [{login}]");

            if (NeedsRehash(player.PasswordHash))
            {
                player.PasswordHash = GetHashedString(password);
                playerCollection.Update(player);
                SphLogger.Info($"SRV: Upgraded the stored password hash of [{login}]");
            }

            return player;
        }

        if (!createOnNewLogin)
        {
            return null;
        }

        // A GM login must not be claimable by whoever connects first.
        if (ServerConfig.AppConfig.IsGmAccount(login) && !ServerConfig.AppConfig.GmAccountBootstrap)
        {
            SphLogger.Error($"SRV: [{login}] is a GM login and does not exist; set GmAccountBootstrap to create it");
            return null;
        }

        SphLogger.Info($"SRV: Adding [{login}] to DB");

        var pwdHash = GetHashedString(password);

        player = new PlayerDbEntry
        {
            Index = playerIndex,
            Login = login,
            PasswordHash = pwdHash,
            Characters = []
        };

        var playerId = playerCollection.Insert(player);

        SphLogger.Info($"SRV: Created player [{playerId}] for [{login}]");

        return player;
    }

    public static bool IsNameValid (string name)
    {
        return !DbConnection.Characters.Exists(x => x.Name == name);
    }

    private static bool IsLockedOut (string login)
    {
        lock (failures)
        {
            if (!failures.TryGetValue(login, out var entry))
            {
                return false;
            }

            if (DateTime.UtcNow - entry.FirstUtc > FailureWindow)
            {
                failures.Remove(login);
                return false;
            }

            return entry.Count >= MaxFailedAttempts;
        }
    }

    private static void RegisterFailure (string login)
    {
        lock (failures)
        {
            if (failures.TryGetValue(login, out var entry) && DateTime.UtcNow - entry.FirstUtc <= FailureWindow)
            {
                failures[login] = (entry.Count + 1, entry.FirstUtc);
                return;
            }

            failures[login] = (1, DateTime.UtcNow);
        }
    }

    private static void ClearFailures (string login)
    {
        lock (failures)
        {
            failures.Remove(login);
        }
    }

    private static bool NeedsRehash (string stored) => !stored.StartsWith(CurrentPrefix, StringComparison.Ordinal);

    private static string GetHashedString (string str)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(str, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{CurrentPrefix}{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool EqualsHashed (string password, string stored)
    {
        try
        {
            if (stored.StartsWith(CurrentPrefix, StringComparison.Ordinal))
            {
                var parts = stored[CurrentPrefix.Length..].Split(':');
                if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < 1)
                {
                    return false;
                }

                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256,
                    expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            var hashBytes = Convert.FromBase64String(stored);
            if (hashBytes.Length != SaltBytes + LegacyHashBytes)
            {
                return false;
            }

            var legacySalt = hashBytes[..SaltBytes];
            var legacyHash = Rfc2898DeriveBytes.Pbkdf2(password, legacySalt, LegacyIterations,
                HashAlgorithmName.SHA256, LegacyHashBytes);
            return CryptographicOperations.FixedTimeEquals(legacyHash, hashBytes[SaltBytes..]);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
