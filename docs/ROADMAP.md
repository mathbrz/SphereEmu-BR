# Roadmap

What to work on next, in priority order, and what still needs a test. Pick something from the top that matches what you can
do, say so on its issue, and open a pull request against `develop` (see [CONTRIBUTING.md](../CONTRIBUTING.md)).
What was already done is in [CHANGELOG.md](CHANGELOG.md). Open questions about the original are in
[OPEN_RESEARCH.md](OPEN_RESEARCH.md), and what already differs is in [KNOWN_DEVIATIONS.md](KNOWN_DEVIATIONS.md).

Priority comes from how much it changes the feel of the game and how much evidence we already have:
**P1** is measured and small, **P2** matters but needs more work or data, **P3** is large or needs new captures.
A maintainer may reorder this list; change it with a pull request like any other file.

## Next up

### P1: measured on the official server, ready to build

| Issue | What | Why now |
|---|---|---|
| [#4](https://github.com/mathbrz/SphereEmu-BR/issues/4) | Respawn 8.3 s after the death hit, with 100 hp, in the nearest city | Measured twice, the same both times. Upstream revives in place after 5 s |
| [#3](https://github.com/mathbrz/SphereEmu-BR/issues/3) | Dying costs about 5.75 percent of title and degree experience | Measured twice, same ratio. Make it configurable |
| [#5](https://github.com/mathbrz/SphereEmu-BR/issues/5) | A city kills a monster that chases a player into it (hp 1, effect 320) | Seen with two different monsters |

### P2: matters, needs more data or work

| Issue | What | Missing |
|---|---|---|
| [#6](https://github.com/mathbrz/SphereEmu-BR/issues/6) | Monster loot by the monster's level | A small tool that lists the items of every opened bag next to the monster that died, then a table from captures |
| [#9](https://github.com/mathbrz/SphereEmu-BR/issues/9) | Refuse attacks on players inside a city (message 10302) | Defining the peaceful zones (the city protection already has them) |
| [#7](https://github.com/mathbrz/SphereEmu-BR/issues/7) | Mutator mantras (defense, jump, cleansing powder first) | One effect record per mutator, and captures of the other kinds |
| [#11](https://github.com/mathbrz/SphereEmu-BR/issues/11) | The distance at which a chasing monster gives up | One measurement session. Aggro looks like about 12 m (5 samples) |

### P3: large, or waiting for a capture

| Issue | What | Waiting for |
|---|---|---|
| [#8](https://github.com/mathbrz/SphereEmu-BR/issues/8) | Missions: generate from the form, reward, dungeon entrance | More samples of the reward by place, participants and level |
| [#10](https://github.com/mathbrz/SphereEmu-BR/issues/10) | Trade between players | A capture of a finished trade with a second player |
| [#12](https://github.com/mathbrz/SphereEmu-BR/issues/12) | Switch character without closing the game | A quick test of what the client does on a closed connection |

### Also known, not filed yet

- Guild castles and their rules: an entry fee paid to the owning clan, a treasury only for members, doors that open with a key
  (see [CAPTURE_FINDINGS_2026-10-25.md](CAPTURE_FINDINGS_2026-10-25.md)).
- The fall animation of a dropped item and the projectile of a thrown powder (only visual).
- Effects of the remaining mantras (about 160 are not implemented).
- Anti-cheat messages (`CheckCrc`) are not handled. Do not change this without a maintainer.

## To test in game

Implemented, but nobody has checked it against the original or in a long session yet. Tick it in a pull request that
fixes or confirms it.

- [ ] The healing mantra sends the "ready again" message when its use time ends (no visible change is expected, and no
  client error should appear).
- [ ] Splitting a stack in the inventory cells with items other than mantras and powders.
- [ ] A powder is consumed by one on use (retail: 108 became 107). Check it on our server.
- [ ] How long an item stays on the ground (we use 5 minutes, upstream's number; retail unknown).
- [ ] Whether clicking an item equips it, for items other than bags and books.
- [ ] A long session (two hours or more) without a client crash, after the pong fix.
- [ ] The paste chance of a mantra into a book by level (unknown, see OPEN_RESEARCH).

## How to help without code

- **Record the official server** with [CAPTURING.md](CAPTURING.md) and share only the numbers you saw (never the capture).
  The most useful: a finished mission with its reward, the distance a monster chases, items dropped by monsters of several
  levels, and a finished trade with another player.
- **Test and report** on the issue tracker with the template for retail behaviour.
