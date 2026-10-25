# Architecture map

Where things live, so a person or an AI assistant can find the right file quickly. Rules for contributors are in
[CONTRIBUTING.md](../CONTRIBUTING.md) and [AGENTS.md](../AGENTS.md).

## Read this first: names that mislead

- **`Client/` is server code.** It holds one object per connected player (`SphereClient`) and the handlers for what
  that player's game client sends. It is not the game client.
- **"The client"** in docs means the original Sphere game program the player runs. We do not ship it.
- **"Retail"** means the official Sphere server. Our behaviour is measured against it, never copied from its code.
- **"Emulator"** is this server. Godot is only the framework it runs on (headless, no window).

## Top-level layout

| Path | What it is |
|---|---|
| `Server/` | Server startup, login and auth, config, debug console, GM command parser, admin UI |
| `Client/` | Per-player session: networking, packet handlers, queued events, event handlers |
| `Shared/` | Code used by both sides: bit stream, database models, packet builders and serializers, world state |
| `Sphere.Game/` | Game objects in the world: monsters, NPCs, loot, missions, game object database |
| `Godot/` | Godot scenes, nodes, terrain, navigation and the asset tree (large, not in the code-only repository) |
| `Sphere.NetworkProtocol/` | Inferred command catalog of every network module (`mbc_commands.jsonl`) |
| `Sphere.PacketDefinitions/` | Packet templates (`.spd`, `.spdp`) for spawned objects |
| `Sphere.GameDataDecode/` | Decoded client data tables and texts (`params`, `language`); game data, not ours |
| `.generated/` | JSON built from the decoded data (items, suffixes, localization) |
| `.solutions/` | Helper libraries and tools: packet classification, object database, packet log viewer |
| `Config/Balance/` | Balance numbers (combat, experience), editable without code changes |
| `Tools/` | Maintenance scripts and converters |
| `docs/` | This documentation |

## Where to look for a feature

| Feature | Start here |
|---|---|
| Login, accounts, password hashing, lockout | `Server/Login/Auth/LoginManager.cs` |
| Server config and GM list | `Server/Config/ServerConfig.cs`, `appsettings.json` |
| GM commands | `Server/Debug/Parser/ConsoleCommandParser*.cs` |
| Incoming packet routing | `Client/Networking/ClientConnection.cs`, `.solutions/SphereHelpers/Networking/ClientPacketClassifier.cs` |
| Outgoing packet builders | `Shared/Networking/CommonPackets.cs` and `Shared/Networking/Packets/` |
| Character data sent at login | `Shared/Networking/DataModel/Serializers/CharacterDbEntrySerializer.cs` |
| Bags, pouches, books and their cells | `Shared/Db/ItemContainers.cs`, `Client/Networking/Handlers/InGame/Items/` |
| Buying and selling | `Client/Networking/Handlers/InGame/Items/BuyItemFromTargetHandler.cs`, `SellItemToTargetHandler.cs` |
| Melee and mantra use | `Client/Networking/Handlers/InGame/DamageHealEffects/DamageTargetHandler.cs` |
| Health changes | `Client/Networking/Handlers/InGame/DamageHealEffects/ChangeCharacterHealthHandler.cs` |
| Mutators (buffs and debuffs) | `Client/Networking/Handlers/InGame/Mutator/MutatorHandler.cs` |
| Monster stats, combat, movement, death | `Sphere.Game/WorldObject/Monster/*.cs` |
| Monster speed per model | `Sphere.Game/WorldObject/Monster/Navigation.cs` (reads `SPDWALK`/`SPDRUN` from the model cfg) |
| City protection from monsters | `Server/GameplayLogic/CityProtection.cs` |
| Game clock | `Server/GameplayLogic/GameClock.cs` |
| Combat and experience numbers | `Server/GameplayLogic/Combat/`, `Server/GameplayLogic/Experience/`, `Config/Balance/` |
| Item and monster data | `.generated/objectData.json`, `Sphere.Game/GameObjectDb.cs` |
| Database (LiteDB, file `sph.db`) | `Shared/Db/` |

## How a player action flows

1. The game client sends a frame to the server on the gameplay channel (300).
2. `ClientConnection` removes the client-side obfuscation and `ClientPacketClassifier` names the frame.
3. The matching handler in `Client/Networking/Handlers/...` applies the rule on the server (the server is
   authoritative) and updates the database.
4. The handler queues the replies with builders from `Shared/Networking`, and other players in range are told.

## Facts that save time

- A frame is a transport header plus a bit-packed body (LSB first). The wire layout is defined by the client's
  network modules, not guessed. See `Sphere.NetworkProtocol/` before assuming a field width.
- The game's database ids, hotkey and container "cells" follow the client's numbering; check an existing handler
  before inventing one.
- Behaviour is either **retail** (measured, with evidence) or **custom** (deliberate difference, configurable).
  Mark which one in the code comment or the pull request.
