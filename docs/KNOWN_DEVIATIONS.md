# Known deviations from the original game

Things this server does that the official server does **not** do, or that nobody has verified. Read this before
treating any behaviour as "how Sphere works". The goal of the project is to match the original, so every row here is
meant to shrink over time.

Source column: **upstream** = comes from knelse/SphereEmu, **fork** = added in this repository, **both** = either.
Evidence column: where the original behaviour is known from (capture, client data) or "unknown".

## Behaviour that differs from the original

| Behaviour | Source | What the original does | Evidence | Notes |
|---|---|---|---|---|
| A dead player revives in a city 8.3 s after the death hit, with 100 hp | fork | Same (Torwel measured twice; other cities use their centre until someone measures them) | retail capture 2026-10-25, two deaths | Built, not yet tried in game. Upstream revives in place after 5 s. `RespawnDelaySeconds` and `RespawnAtCity` in `appsettings.json` switch it back |
| Server keepalive "pong" built from an echo of the client's position frame | upstream | The official server answers with real messages | none | A workaround to keep the client alive. Its counter byte is read by the client as a message region; region 6 crashed the client twice in 30 minutes. The fork skips that counter value (`PingHandler.SendPositionStreamPong`). To be reported upstream |
| Monsters attack every `UseTime` of the catalog | upstream | Gap between two hits is about `UseTime + 1.2 s` and varies by about 20 percent of the UseTime (UseTime 4: 4.4 to 6.0 s; median of all 5.8 s) | retail captures, 56 hits | The fork adds 1.2 s and the jitter, a fit to 56 samples, not a formula known from the client |
| Monsters attack players of any level | upstream | A monster 20 or more levels below the player leaves it alone unless hit first | retail captures (no attacker below level 41 against a level 60 player) | Implemented in the fork (`Monster.IgnoresPlayer`) |
| Monsters do not enter the radius of a city centre (`CityMonsterProtection`) | fork | Unknown; monsters in cities are rare in the original | none | A convenience for testing, configurable and switchable with `/safezone` |
| A player can hit themselves (self-targeted attack) | upstream | Alt-click on yourself hits, but the rules are not captured | unknown | The fork's old `AllowSelfAttack` switch was dropped in the sync |
| Selling an item pays about a tenth of its buying price | fork | Same | retail capture (sale offers) | Not a deviation; listed because it looks like a bug |
| An item nobody picks up vanishes from the ground after 5 minutes (and on a restart) | fork | Vanishes after a while; the exact time is not measured | the player who knows the original | Upstream returned it to the owner. The 5 minutes are upstream's number |
| Underwater breath damage every 6 s | upstream | The original has drowning, the numbers are not captured | unknown | |
| Revive protection of 5 s | upstream | A few seconds of invulnerability, longer in the captures | retail capture (about 8 s) | |
| Time between two uses of a mantra | fork | Depends on the stack: `UseTime x (2 - min(stack, cap) / cap)`, cap = 2 x the mantra level | client tooltip, 14 readings of 3 mantras, all within 0.05 s | Implemented for the healing and prana mantras (`DamageTargetHandler.MantraUseSeconds`); the server uses 90 percent of it as a tolerance. A prana mantra is paid in health (`HpCost`) |
| Dying costs experience only when `DeathExperienceLossPercent` is set (default 0, off) | fork | Title and degree experience both drop by about 5.75 percent | retail capture, two deaths, one level 31 character | Built, not yet tried in game. Off by default because the percent is known for one level only. Set 5.75 to match the sample |
| A monster that chases a player into a city is only kept out | fork | The city sets its hp to 1 with effect 320 and it dies 2.5 to 4 s after its last hit | retail capture, two kills | Not implemented |
| No player-versus-player rules, no trade between players, no missions, castles or dungeons | upstream | Peaceful areas refuse attacks (message 10302), the player trade window works as an NPC shop, missions are generated from a 3-byte form | retail capture | See the findings file for the wire details |
| The healing mantra did not announce that it was ready again | fork | `cmd67` arrives once the use time ends | retail capture | Implemented in `DamageTargetHandler.HealWithMantra` |

## Test conveniences that never existed in the original

- **GM commands** (`/give`, `/gold`, `/lvl`, `/tp`, `/die`, ...): custom. Work only for logins in `GmAccounts` and with
  `DebugMode` on. See [GETTING_STARTED.md](GETTING_STARTED.md).
- **Start money** (`Spawn_Money` in `appsettings.json`): a test value.
- **The two test mutators** sent at login by `ClientConnection` (jump x4 and one more): a leftover of upstream's tests.

## How to use this file

- A contribution that fixes a row should remove or update it and cite the evidence.
- A new behaviour that is not known to match the original must be added here, marked "unknown" or "custom".

## Rules taken from a player of the original (not from a capture)

These are implemented here because someone who played the official server described them. They have no capture
behind them, so correct them if a capture or the client data says otherwise.

| Rule | Where |
|---|---|
| Mantras stack, and a mantra dropped on an identical one in a book cell adds to it | `ContainerMoveHandler.StackOnto`. The request is a 9-byte UseItem naming the destination (3 in the retail capture, same shape here); retail answers with a count-only `UpdateContainerSlot` (5 bytes) in the destination |
| A mantra taken out of its book and thrown away is destroyed, it does not land on the ground | `DropItemToGroundHandler` |
| An item nobody picks up vanishes from the ground after a while (5 minutes here, the real time is unknown) | `GroundItem.Expire` |
| Clicking a bag or a book opens it where it is; equipping is a drag | `UseItemHandler` |
| A prana mantra is paid in health | `DamageTargetHandler.HealWithMantra` |
