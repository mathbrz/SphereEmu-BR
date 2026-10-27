# Origin of this code

This repository is a community fork of **SphereEmu** by Andrey Klyuev (GitHub: [knelse](https://github.com/knelse)),
licensed under Creative Commons Attribution-NonCommercial-ShareAlike 4.0 (CC BY-NC-SA 4.0): credit is required, use must stay non-commercial, and changes must keep the same license. The original author and contributors keep their copyright; see [LICENSE.md](../LICENSE.md).

- Upstream: https://github.com/knelse/SphereEmu

## Credits

The work this fork stands on, as the upstream maintainer credits it in his changelog:

- **knelse**: the server emulator, the protocol work, the world, monsters, items and most of the tools.
- **vpopus**: reading the game's world files (99 percent of the work of the world import into Godot, in the maintainer's words).
- **Goethe**: the packet event recognition in the packet viewer and the protocol catalog behind it.
- **Barahlush**: the working inventory with stat calculation, the weapon damage calculation and the rewrite of the client packet parsing.

Thanks to all of them. If you know of someone who should be listed, say so in an issue.

## Independence

This repository continues the original code from commit `b88f5fd21` (2026-10-21), "Сервер не ждёт вертикальную синхронизацию монитора (#67)",
and is maintained on its own. The original project is moving to a new network protocol and to closed development, so this
repository does not follow it and no longer depends on it:

- The server never contacts the original repository at run time. The self-updater of the original project is off unless you
  set `SPHEREEMU_UPDATER=1`, and its release feed points at this repository, which publishes none.
- The only thing taken from the original repository is the set of game files, once, by `Tools/setup-assets.ps1`. If that
  repository is gone or private, point the script at a full clone you hold with `-Source` (see
  [GETTING_STARTED.md](GETTING_STARTED.md)). Making the game files from the player's own client is on the
  [roadmap](ROADMAP.md).
- The history of this repository starts as a snapshot, because the original history holds large game files that are not
  redistributed here.

The fork started from `5bc45556d` (2026-09-21) and was brought up to that commit with a three-way merge.

## Reported to upstream (2026-10-25)

The maintainer asked us to talk on Discord before sending pull requests, because changes are planned on his side, so none
are open yet. Two problems found in his tree are filed as issues, each with the fix this repository uses:

- [knelse/SphereEmu#76](https://github.com/knelse/SphereEmu/issues/76): the keepalive pong counter can be read by the client
  as a region 6 message and crash it (`Client/Networking/Handlers/InGame/PingHandler.cs`).
- [knelse/SphereEmu#77](https://github.com/knelse/SphereEmu/issues/77): a jewelry vendor shows an empty shop because
  `GetItemForTier` throws when a tier has no matching object (`Sphere.Game/NpcTrade/ItemsOnSale/ItemsOnSaleGenerator.cs`).

## Not included here (fetched by `Tools/setup-assets.ps1`)

Game files and game-derived data are not part of this repository: client, models, textures, terrain, decoded data
tables, generated item JSON, and world scene chunks. Each player uses their own copy of the game. See
[DISCLAIMER.md](../DISCLAIMER.md).
