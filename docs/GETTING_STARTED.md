# Getting started

Languages: English | [Português](GETTING_STARTED.pt-BR.md)

How to run the server on your own computer and play on it. This is a test server for the community, not the
official game.

## What you need

| Item | Notes |
|---|---|
| Windows 10 or 11 | The game client is Windows only |
| Your own copy of the Sphere client | Install it from the official source. We do not distribute it |
| .NET 9 SDK | The exact version is pinned in `global.json`. Check with `dotnet --version` |
| Godot 4.7 Mono (or newer) | Needed to run and build the server. Players who only join do not need it |
| Git | To get and update the code |
| The game files | Game data and world files the server needs. They are not in this repository; step 2 fetches them from the original project's public repository |

## 1. Get the code

```bash
git clone https://github.com/mathbrz/SphereEmu-BR.git
cd SphereEmu-BR
```

The repository is private for now. Ask a maintainer for access first.

## 2. Fetch the game files

```powershell
powershell -ExecutionPolicy Bypass -File Tools\setup-assets.ps1
```

The script downloads about 3.8 GB from the original project's public repository (knelse/SphereEmu), at the exact commit
this code is based on, and places `Godot/Models`, `Godot/Textures`, `Godot/Terrain`, `Godot/World`,
`Sphere.GameDataDecode`, `.generated` and `sphereclient_patched.exe` next to `Server/` and `Client/`.
This project hosts none of these files. Do not commit them: the `.gitignore` already keeps them out. If the
original repository ever disappears, the script stops working; keep a copy of what it downloaded.

The first time the server runs, Godot imports the models and textures, which can take several minutes.

## 3. Build and run the server

```bash
dotnet build SphServer.sln
```

The easiest way is the script, which finds Godot, starts the server hidden and waits until it is ready:

```powershell
powershell -ExecutionPolicy Bypass -File Toolsserver.ps1 start -Build   # build, then start
powershell -ExecutionPolicy Bypass -File Toolsserver.ps1 status        # running? how many players?
powershell -ExecutionPolicy Bypass -File Toolsserver.ps1 restart -Build # after changing code
powershell -ExecutionPolicy Bypass -File Toolsserver.ps1 stop
```

It only touches the Godot processes started from this folder, and **it refuses to stop or restart while a player is
connected** (positions are saved on logout; ask them to log out from the game menu, or add `-Force`). If it cannot find
Godot, pass `-GodotPath "<path to Godot_v4.7.x-stable_mono_win64_console.exe>"` or set the `GODOT_PATH` environment variable.

By hand, the same thing is:

```powershell
& "<path to Godot_v4.7.x-stable_mono_win64_console.exe>" --headless --path "<path to SphereEmu-BR>"
```

The server is ready when the newest file in `logs/` (`server_<date>.log`) contains
`Server up, waiting for connections...`. It listens on port `25860`. The first start takes a few seconds.

To stop it, close that process. Before restarting while someone is connected, ask them to **log out from the game
menu** first: positions are saved on logout.

## 4. Point the game client at your server

In the folder where the client is installed, edit `connect.cfg` and `connectn.cfg` and set:

```
MAIN_URL "127.0.0.1"
PORT 25860
```

Use `127.0.0.1` when the server runs on the same computer. For a friend's server, use that computer's address.
Keep a copy of the original files, so you can go back to the official server.

Start the client with the `/login` argument, using the `sphereclient_patched.exe` that step 2 placed in the repository
folder (copy it into the game folder, next to `sphereclient.exe`):

```
sphereclient_patched.exe /login
```

Never share your `connect.cfg`: it stores your login in obfuscated form.

## 5. Create your account and play

On the login screen type any login and password. **The first login creates the account.** Choose a password you do
not use anywhere else, and remember it: there is no reset yet. Then create a character and enter the world.

Wrong passwords are limited: after 5 failures in 5 minutes that login is locked for a while.

## 6. Become a GM (your own server only)

GM commands are for testing. They only work when `DebugMode` is `true` in `appsettings.json`.

1. Open `appsettings.json` in the repository folder.
2. Put your login in `GmAccounts` (several logins separated by commas):
   ```json
   "GmAccounts": "yourlogin",
   ```
3. If the account does **not exist yet**, also set `"GmAccountBootstrap": "true"`, start the server, and log in once
   with that login to create it. Then set `GmAccountBootstrap` back to `"false"` and restart the server. If the account already exists, you do not need the bootstrap.
4. In the game, type commands in the chat box, starting with `/`.

Why the bootstrap exists: without it, on a new database the first person to log in under a GM name would become GM.
On a shared server, list only trusted administrators and use strong passwords.

## Common GM commands

Type a command with no arguments to see its usage when it needs some.

| Command | What it does |
|---|---|
| `/lvl <title> <degree>` | Set your level |
| `/gold <amount>` | Set your money |
| `/attr <value>` or 8 values | Set attributes |
| `/stats ...` | Set stat values |
| `/give <game_object_id> [count]` | Create an item |
| `/giveinv`, `/giveinvns` | Put an item in the inventory (by id, or by name with suffix and tier) |
| `/bag` | Create a bag |
| `/inv` | List what the server holds for you |
| `/clearinv` | Empty the inventory |
| `/mob`, `/mobid` | Spawn a monster |
| `/loot` | Loot test |
| `/tp`, `/pos` | Teleport, show position |
| `/die` | Kill your character |
| `/karma`, `/guild`, `/clan` | Set karma, guild, clan |
| `/buff` | Apply a buff |
| `/time [day\|night\|HH[:MM]]` | Set the game time |
| `/safezone`, `/ghost` | Toggle city protection, ghost mode |

Every GM command is logged in `logs/server_*.log` with the player name.

## Settings worth knowing

All in `appsettings.json`. The comments in the file explain each one. The common ones: `Port`, `DebugMode`,
`GmAccounts`, `GmAccountBootstrap`, `AllowSelfAttack`, `MobChaseEnabled`, `CityMonsterProtection`, `Spawn_Money`.

## Problems

| Symptom | What to check |
|---|---|
| Client cannot connect | The server is running (`Server up` in the log), the port is `25860`, `MAIN_URL` is right, nothing else uses the port |
| Login returns to the login screen | Wrong password, or the lockout is active (wait a few minutes). The log says which |
| GM commands do nothing | `DebugMode` is `true`, your login is in `GmAccounts`, and the message starts with `/` |
| Server will not start | `dotnet build` passes, and step 2 finished (the folders `Godot/Models`, `Sphere.GameDataDecode` and `.generated` exist) |
| Something looks wrong in game | Open an issue with the log lines around it. See [CONTRIBUTING.md](../CONTRIBUTING.md) |

Do not share `sph.db`, logs or captures: they hold accounts and chat.
