# Open research

Things this server does not know yet about the original game, and what would settle each one. Behaviour already
decided is in [KNOWN_DEVIATIONS.md](KNOWN_DEVIATIONS.md). When an item is settled, move it there with its evidence.

| # | Question | What we have | What would settle it |
|---|---|---|---|
| 1 | How likely is a mantra to stick when pasted into a book, and does it depend on the character level or the mantra level? | The client has the messages "memorized", "not memorized" and "lost" (`_sys`). No formula or number in the client data. Here pasting always works | Counts from the official server: character level, mantra level, tries, successes. Use cheap mantras the vendors sell (levels I to XII); rare ones are not needed. About 30 tries per row. Not yet done |
| 2 | At what distance does a monster stop chasing and go home? | Attack range and interval are measured. The give-up distance is not. The fork uses upstream's 30 m | Positions of monsters and the player in the captures (analysis started, see `SphereRE/analysis`), or a measurement on the official server |
| 3 | After how long does an item lying on the ground vanish? | Players say it vanishes. The fork uses 5 minutes (upstream's number) | A stopwatch on the official server |
| 4 | Does clicking an item equip it? | A player says no (equipping is a drag). Fixed for bags and books only; other items still equip on click (upstream) | A capture or a player test with a sword and an armour |
| 5 | Does the client already wait the tooltip time between two mantra uses, or must the server enforce it? | The tooltip time is known (see KNOWN_DEVIATIONS). The server keeps 90 percent of it as a floor | Time two real heals with a single mantra and compare with the tooltip (4.5 s for level XII) |
| 6 | Largest stack of a mantra | The server allows up to 65535 | A player who stacked many |
| 7 | What happens to a map or a recipe taken out of its book and thrown away? | Only the mantra book is known (destroyed). The others fall on the ground here | A player who remembers |
| 8 | Why does the red marker appear on every cell when unequipping an item? | Open since the first tests. Upstream's rewrite of the character data packet may have fixed it; not retested | Test unequipping a shield and a sword on the current build |
| 9 | Respawn in a city chosen by karma | Measured in a capture. Upstream respawns in place; the fork lost its city respawn in the sync | Port it back on top of upstream's revive |
| 10 | Merging two stacks of the same item in the inventory (not only in a book) | Only mantras in a book merge. Upstream has open pull requests for stacks | A player's description of the inventory rules |
| 11 | Effects of the other 160 mantras (buffs, debuffs, damage) | Only healing and prana mantras work. The 132 with a mutator need the mutator object, which is not built | Captures of casts, or players describing each kind |

## How to report a result

Say what you did, the exact numbers you saw, and on which build (official or this server). One honest sentence such
as "felt faster, not timed" is useful if it is marked as an impression.

## Settled or updated on 2026-10-25

- **#9 Respawn city:** settled by a capture. The revive comes 8.3 s after the death hit, with 100 hp, in the nearest city (a
  death inside a castle also respawns in the city). See [ROADMAP.md](ROADMAP.md) (P1) and
  [CAPTURE_FINDINGS_2026-10-25.md](CAPTURE_FINDINGS_2026-10-25.md).
- **#10 Merging stacks in the inventory:** works now for any counted item (see [CHANGELOG.md](CHANGELOG.md)).
- **#2 Give-up distance:** aggro starts at about 12 m (five monsters). The give-up distance is still not measured.
- **#11 Other mantras:** defense, high jump and the cleansing powder are measured (see the findings file).
