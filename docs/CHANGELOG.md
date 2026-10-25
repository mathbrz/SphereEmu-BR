# Changelog

What has been done, newest first. The list of what comes next is in [ROADMAP.md](ROADMAP.md).
"Verified" means a person tried it in game; the rest is built and compiled but not tried by anyone yet.

## 2026-10-25

### Added
- Splitting a stack into an empty cell of a bag or a book, with the answer copied from the official server. Verified.
- Splitting a stack in the inventory cells. Verified. Custom: the official answer for these cells was not captured.
- Joining any counted item (stones, powders, herbs), not only mantras. Verified.
- The healing mantra tells the client when it is ready again, as the official server does.
- A guide to record and read captures of the official server ([CAPTURING.md](CAPTURING.md)) and a record of what a live
  session showed ([CAPTURE_FINDINGS_2026-10-25.md](CAPTURE_FINDINGS_2026-10-25.md)).
- A script to start, stop and check the server (`Tools/server.ps1`), and community texts for Discord in three languages.

### Fixed
- Stacks in a book showed a wrong size: the cell value is the stack's weight, not its count. Verified.
- A jewelry vendor opened an empty shop because the item draw threw on a tier with no matching object. Verified.
  (Upstream has the same fault; reported as knelse/SphereEmu#77.)

### Repository
- New public repository with a clean history, code only. License text corrected to CC BY-NC-SA 4.0.
- `main` and `develop` are protected for everyone: pull requests only, with a passing build. Secret scanning is on.
- A README written for newcomers and AI assistants, eleven starter issues, and this roadmap and changelog.

## 2026-10-24

### Added
- Mantras stack in a book, and a mantra thrown out of its book is destroyed. Verified.
- The time between uses of a mantra follows the stack size (the tooltip formula), and prana mantras cost health. Verified.
- An item nobody picks up vanishes from the ground after a while instead of returning to its owner. Verified.
- The monster attack interval varies the way the original does (the delay plus 1.2 s, with some jitter), and a monster far
  below the player's level leaves the player alone. Both measured from retail captures.
- Clicking a bag or a book opens it where it is; equipping is a drag. Verified.
- `Tools/setup-assets.ps1` fetches the game files from the original project's public repository, so the repository holds code only.

### Fixed
- Monsters stood still: the terrain and navigation data were missing from the fetched files. Verified.
- The client crashed now and then because the keepalive pong counter could be read as a region 6 message. Verified.
  (Upstream has the same fault; reported as knelse/SphereEmu#76.)
- Books stopped taking their own kind of item after the slot table changed. Verified.
- Items the upstream drop left on the ground can be taken into bag cells. Verified.

### Brought from upstream
- The fork was synchronised with upstream `b88f5fd2` and keeps what only this fork has: the hardened login (slow password
  hash, lockout, GM bootstrap guard), items inside bags, pouches and books, hotkey drop, healing and prana mantras, and the
  buy and sell of big stacks.
