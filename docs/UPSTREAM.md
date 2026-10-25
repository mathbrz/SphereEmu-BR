# Origin of this code

This repository is a community fork of **SphereEmu** by Andrey Klyuev (GitHub: [knelse](https://github.com/knelse)),
licensed under Creative Commons Attribution-NonCommercial-ShareAlike 4.0 (CC BY-NC-SA 4.0): credit is required, use must stay non-commercial, and changes must keep the same license. The original author and contributors keep their copyright; see [LICENSE.md](../LICENSE.md).

- Upstream: https://github.com/knelse/SphereEmu
- Last synchronised with upstream commit `b88f5fd21` (2026-10-21), "Сервер не ждёт вертикальную синхронизацию монитора (#67)".
  The fork started from `5bc45556d` (2026-09-21) and was brought up to that commit with a three-way merge. The history of
  this repository starts as a snapshot (the original history contains large game assets that are not redistributed
  here), so later upstream changes are read and ported or merged by file, not by branch.

To see what upstream did since our base:

```bash
git remote add upstream https://github.com/knelse/SphereEmu.git
git fetch upstream
git diff -w --ignore-cr-at-eol b88f5fd21 upstream/master -- <path>
```

Fixes that are not specific to this community are offered back to upstream as pull requests
(see the introduction in knelse/SphereEmu#75).

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
