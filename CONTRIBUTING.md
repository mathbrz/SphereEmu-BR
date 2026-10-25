# Contributing

Languages: English | [Português](CONTRIBUTING.pt-BR.md) | [Italiano](CONTRIBUTING.it.md)

Thanks for helping. This project is a Sphere server emulator. The goal is to behave like the original game, so
every behaviour change needs evidence.

## Branches

| Branch | Purpose | Who writes to it |
|---|---|---|
| `main` | Stable base. Always builds and runs. | Maintainers, only through reviewed pull requests |
| `develop` | Integration of accepted contributions. | Maintainers |
| `feature/*`, `fix/*` | Your work, in your own fork. | You |

Open pull requests against `develop`. `main` is updated from `develop` when it has been tested.

## Language

- Code, comments, commits, issues and pull requests: **English**. The upstream author reads them.
- Community talk, guides and support: Portuguese is fine.

## What a good change looks like

1. **One problem per pull request.** Small and focused. Unrelated cleanups go elsewhere.
2. **Evidence.** For any behaviour change, say where the behaviour comes from, one of:
   - a capture of the official server (packet log, with account data removed),
   - the client data (`params`, `language`, `.mbc` modules),
   - a decompiled client routine,
   - or "custom": a deliberate difference from the original, stated as such and, if it is a server rule, behind a
     setting in `appsettings.json`.
3. **Match the code around it.** Follow `.editorconfig` and the naming in the file you edit. Do not reformat files.
4. **Comments say why**, not what. Keep them short.
5. **Build passes:** `dotnet build SphServer.sln` with no errors.
6. **Wire format changes** are checked against the protocol schema. Do not guess widths; read the module definition
   and the client path first (`Sphere.NetworkProtocol/`).

## Do not commit

- Game assets that are not yours to redistribute (client files, models, textures, sounds).
- Packet captures, `sph.db`, logs, or anything containing logins, passwords, hashes or chat.
- Secrets of any kind.

The `.gitignore` covers the common cases. If in doubt, ask in the pull request.

## Using AI assistants

Welcome, with the same rules as humans:

- You are responsible for what you submit. Read the diff before opening the pull request.
- The assistant must follow `AGENTS.md`.
- Say in the description what you ran and what you saw. "It compiles" is not "I tested it in game".
- Do not let an assistant invent protocol details. If a field is unknown, mark it unknown.

## Areas that need a maintainer's approval

Authentication and account handling, GM commands and permissions, anti-cheat and validation, database schema,
network framing and encryption. Open an issue first.

## Review

Every pull request is checked against [docs/PR_REVIEW_CHECKLIST.md](docs/PR_REVIEW_CHECKLIST.md), safety first.

## Reporting bugs

Include: what you did, what you expected, what happened, the server log lines around it (`logs/server_*.log`), and
the time. A short description of the character (level, items) helps. Remove any account data first.

## Staying in sync with upstream

Maintainers read what the original project did through its own remote (see [docs/UPSTREAM.md](docs/UPSTREAM.md)), port the changes
they want in a separate branch (this repository does not share history with it, so they are ported, not merged), build,
test, and only then open a pull request into `develop`. Fixes that are not specific to this community should also be sent upstream as pull requests.
