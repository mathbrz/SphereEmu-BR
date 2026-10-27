# SphereEmu-BR

[![PR build](https://github.com/mathbrz/SphereEmu-BR/actions/workflows/pr-build.yml/badge.svg)](https://github.com/mathbrz/SphereEmu-BR/actions/workflows/pr-build.yml)
[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/license-CC%20BY--NC--SA%204.0-lightgrey.svg)](LICENSE.md)
![Not for profit](https://img.shields.io/badge/fan%20project-not%20for%20profit-blue.svg)

A community server emulator for **Sphere** (the old Russian MMORPG), built to behave like the original, with every
behaviour backed by evidence from the original game. It continues [knelse/SphereEmu](https://github.com/knelse/SphereEmu)
(CC BY-NC-SA 4.0) from commit `b88f5fd` and is maintained on its own: nothing here needs the original repository at run
time. Written in C# on Godot 4.7 Mono and run headless.

> **Fan project, not for profit, not affiliated with the owners of Sphere.** This repository holds **code only**.
> No game file (client, models, textures, data) is stored here. See [DISCLAIMER.md](DISCLAIMER.md).

## Start here

| I want to | Go to |
|---|---|
| Run the server and play on my computer | [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md) ([Português](docs/GETTING_STARTED.pt-BR.md)) |
| Contribute a fix or a feature | [CONTRIBUTING.md](CONTRIBUTING.md) ([Italiano](CONTRIBUTING.it.md), [Português](CONTRIBUTING.pt-BR.md)) |
| Work with an AI assistant | [AGENTS.md](AGENTS.md) and [docs/AI_QUICKSTART.md](docs/AI_QUICKSTART.md) |
| See what to work on next, and what was done | [docs/ROADMAP.md](docs/ROADMAP.md) and [docs/CHANGELOG.md](docs/CHANGELOG.md) |
| Find my way around the code | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| See what still differs from the original | [docs/KNOWN_DEVIATIONS.md](docs/KNOWN_DEVIATIONS.md) and [docs/OPEN_RESEARCH.md](docs/OPEN_RESEARCH.md) |
| Record how the official server behaves | [docs/CAPTURING.md](docs/CAPTURING.md) |
| Report a bug or a missing behaviour | [Open an issue](https://github.com/mathbrz/SphereEmu-BR/issues/new/choose) |
| Join the community | [docs/COMMUNITY.md](docs/COMMUNITY.md) |
| Everything else | [docs/INDEX.md](docs/INDEX.md) |

## Quick start

You need Windows, Git, the .NET 9 SDK (the version in [`global.json`](global.json)), Godot 4.7 Mono to run the server,
and your own copy of the game client.

```powershell
git clone https://github.com/mathbrz/SphereEmu-BR.git
cd SphereEmu-BR
powershell -ExecutionPolicy Bypass -File Tools\setup-assets.ps1      # fetches the game files (about 3.8 GB)
dotnet build SphServer.sln
powershell -ExecutionPolicy Bypass -File Tools\server.ps1 start      # starts the server and waits until it is ready
```

Then point the game client at `127.0.0.1` and log in: the first login creates your account, and you create your own
characters in the game. The full steps, with how to become a GM on your own server, are in the guide above.

The setup script downloads the game files from the original project's public repository at the exact commit this code
is based on. Nothing of the game is hosted here.

## How the project works

- **Evidence first.** A change in behaviour needs a capture of the official server, the client's data, or a
  measurement. Without one, it is marked "custom" and made configurable.
- **Small, reviewed changes.** Fork, branch, open a pull request against `develop`. The build runs automatically and a
  maintainer reviews it. Nobody pushes directly to `main` or `develop`.
- **Safety.** No passwords, captures, `sph.db`, logs or game files are ever committed or shared. Authentication, GM
  permissions, anti-cheat and the database schema are not changed without a maintainer. See [SECURITY.md](SECURITY.md).
- **Languages.** Code, commits, issues and pull requests are in English. Guides and community talk also exist in
  Portuguese and Italian.

## Compatibility note (low frame rate in cities)

Running Sphere on recent hardware (notably Radeon 7000 series) can give about 15 fps in cities. dgVoodoo2 can help:

1. Download the latest release from <https://github.com/dege-diosg/dgVoodoo2>, run it and add a profile for Sphere.
2. In the General tab choose Direct3D 12 (feature level 12.0) as the output API; in the DirectX tab choose
   "dgVoodoo Virtual 3D Accelerated Card" with 512 to 4096 MB of VRAM.
3. Copy `D3D9.dll` (or all files) from `MS\x86` to the game folder, then start the game. Cities should reach 150+ fps.
4. If it does not work, try other output APIs or video cards.

## Credits and license

Based on [knelse/SphereEmu](https://github.com/knelse/SphereEmu) by Andrey Klyuev and its contributors. The code is
licensed under CC BY-NC-SA 4.0 ([LICENSE.md](LICENSE.md)): credit the original authors, keep it non-commercial, and keep
changes under the same license. "Sphere" and everything of the original game belong to their owners.
