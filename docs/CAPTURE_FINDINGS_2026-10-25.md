# Retail capture findings, 2026-10-25

Source: one live session on the official server through `captures/LogProxy`, decoded with `captures/DecodeCapture`
(file `sessao3_oficial.jsonl`, never committed: it holds the player's login). Character level 60/60, VIP account.
This file lists what was seen and where the emulator stands.

## Stacks and containers

| Action | What retail does | Emulator |
|---|---|---|
| Split part of a stack into an empty bag/book cell | Client sends container module cmd 4, 9-byte payload `[u24 player][u8 0][u8 destination cell][u8 source cell][u24 amount]`. Answer: cell count (it is the stack WEIGHT, 23 mantras of weight 3 show 69), load, item amount, both cells set, then the new item's record | Implemented (`SplitStackFrame`, `ContainerMoveHandler.HandleSplit`); not yet tested in game |
| Split in the inventory cells | Not captured; the move frame carries the typed amount in byte 23 (shifted one bit), seen for 1 and 3 | Implemented from the fork's own tests (`MoveItemHandler.SplitStack`), custom until a capture exists |
| Join two stacks | UseItem of 9 bytes naming the destination. Answer: source cell released, destination cell shows the new total weight | Implemented; the destination count is now the weight |
| Throw a stack on the ground | One request, no quantity box, the whole stack goes. The item then sends a `TransformUpdate` about every 0.25 s while it falls and settles | Whole stack goes; the fall animation is not copied |
| Pick it up | Same move request as into an inventory cell, answered with the whole stack in the cell | Same |
| Use a mantra | Mantras are NOT consumed by use. Powders are (108 became 107) | Mantra not consumed; check powders |

## Mantras and mutators

- Use of a healing mantra on yourself: weapon init (take it in hand), then UseItem. Answer: hit `+1810` HP, a short-lived
  effect item (`virus` module) in player cell 36, then `cmd67` after the use time. The use time is the tooltip time:
  2.3 s for a stack of 42, seen as 2.36 s between the cast and `cmd67`.
- A buff mantra (defence IX, 18 in the stack, 200 mana, 3.0 s use time, range 4 m): a `virus` item in cell 36 with packed
  state value 290 (the bonus), placed in the player's mutator bag, then `cmd67`, `PlayEffect 260`, and `SetStat pd` updated
  (physical defence rose by 290). Duration shown in the tooltip: 2 hours.
- High jump mantra (Pranah mahanath VII, 210 mana, 2.4 s, 45 min, jumps x4): same shape, effect item in cell 37, `PlayEffect 262`.
  A second use REPLACES the effect (a new item in the same cell), it does not stack.
- Cleansing powder (II, 60 mana, 2.0 s, range 6 m, "removes one mutator"): a projectile (`FlyWeapon`, shot 7) flies to the
  target, the old effect item is hidden and its cell released, a new effect item (cell 38) appears and disappears about 3 s later,
  the stack loses one.
- Emulator: the healing mantra now sends `cmd67` after its use time (`DamageTargetHandler.HealWithMantra`). Not implemented: `cmd66` in the request and the mutator effect items (defence, jump, cleansing powder), which need one effect record per mutator.
  Mantra data (cost, use time, range, duration) comes from the client tooltip and the catalog.

## World

- Portals between continents (Hyperion and Rhodes): the client announces the new position by itself (its own position
  frame jumps), sends an 18-byte `OpenTable` style message, and the server answers with every object of the destination
  area in one burst. No refusal or confirmation in either direction. The account was VIP; whether non-VIP accounts are
  refused is unknown.
- Shift-click on a monster shows its description in the client with no request to the server.
- A monster far below the player's level shows a blue bar and does not attack (consistent with `Monster.IgnoresPlayer`).
- The Rhodes Helmet (`ar_helm_pr`, VIP item, "Premium level 1") scales its stats with the character level (level 60:
  +946 life, +330 attack and defence, +15 each attribute). It exists in `.generated/objectLocalization.json`.
  Whether the scaling is in our data or in the server's records is not checked.

## Still to measure

- The distance at which a chasing monster gives up (needs a monster of similar level, not a blue one).
- Death and respawn on the current character (already captured on 2026-10-03).
- Whether a non-VIP account may use the continent portals.

## Combat, death, loot and missions (second character, level 31, same session)

- Death (level 31 character, killed by a level 80 monster): hits of about 900 every 5 to 12 s, then a hit of -100000 with
  flags 7 (the death marker). 2.6 s later `SetStat hp 0` together with the reduced `title_xp` and `degree_xp`: the penalty
  is about 5.75 percent of both (title 13866 to 13070, degree 409010 to 385503). The client prints "Title stamps -796" and
  "Grade marks -23507". Revive 8 s after the death hit: marker `flags=6`, hp 100, position at the nearest city (Torwel),
  `ClearActivityState`, then the whole character is declared again (every worn item, bags, books). The emulator revives
  in place after about 5 s and takes no experience.
- City protection: a monster that chases the player into the protected area is killed by the city, 2.5 to 4 s after its
  last hit: its hp is set to 1 and effect 320 is attached, then it dies (skeleton 1792 hp, Green Dragon 40960 hp). The emulator
  only keeps monsters out of the city radius.
- Area powder (fireballs VIII, 76 mana, 491 to 1145 damage, 2.8 s, range 20 m, area 7 m): the client sends one frame with the
  animation and a list of targets (`Detonate`, 11 entries for 9 monsters). The server answers per monster, the stack loses 1,
  mana drops, karma and degree xp rise per kill (about +8 each for level 1 monsters).
- Loot: seven `ct_mbag` objects appear on the ground 3.5 s after the kills. Opening one is `UseItem` plus `ReceiveUser`;
  the server answers with the cell list and one item record per item. Taking an item is the ordinary container move.
- Heal from another player: `Health +1050 (name)` twice, the heal mantra of a friend, 5 s apart.
- Missions: the client sends a 3-byte form to the NPC as `PlaceItem` (byte 1 = what you need: title 0, degree 1, money 2;
  byte 2 = where: nearby 0, Hyperion 1; byte 3 = participants), seen for `00 01 01`, `02 00 04` and `01 01 04`. The server
  creates a `quest` item in player cell 25 and sends system message 75. A second request too soon is refused with
  message 173 and no item. Throwing the quest item away abandons it (the item vanishes, not on the ground). Mission text
  lives in the client files (`Sphere.GameDataDecode/language/_tpl_*`), filled with a generated name. The reward and the
  choice of monster are not decoded yet.
- Player to player trade: three client frames (`UseItem` on the player, `ReceiveUser`, `TradeWith`), then the server sends
  everything the other player carries (books, bags, workshop) for the sale and purchase windows. Not implemented.
- A chest in a city (17:41) led to a guild castle: a click on a `ct_chest2` (id 7AE1) was answered with an empty container, and within 1 s
  the client reported a new position (1327, 1102, 1115), an interior far above ground level (155). The area holds 6 `door1`,
  2 monsters, 1 `telep4` (a teleporter), a `cs_chest` (the guild treasury), a `cs_knot`, a `cs_table`. Clicking the treasury
  (`UseItem` plus `ReceiveUser`, 17:44:58) is answered with an echo of `ReceiveUser` and the game refuses a player who is not in the
  guild. Guild castles and a teleporter are official features (an earlier note here guessed otherwise and was wrong). Entering costs a fee of 100 gold paid to the clan that owns the castle (chat: "You have paid a 100 fee to the clan Legacy", purse 35898 to 35798), the treasury refuses non-members ("You can't open the treasury") and the doors open with a key ("The door opens with a key").
- Mission dungeon (title mission, nearby, 1 participant; request bytes `00 00 01`): the entrance object (`entry`, id 12B8) is used with the
  quest item as the key (`UseItem` extra id = quest item). The server teleports the player with `SetCoordinates` into the instance
  (-2072, 4501, 1285), then sends a burst of `CheckCrc` anti-cheat messages (the client answers). The instance has a
  labyrinth (`labyr`), chests (`ct_lab`), doors (`door2`), monsters that appear a few at a time, traps ("The trap", -32 hp
  every second, `hit_type` 3), a mini-map and a prana source ("Prana +33 (Glow)"). Killing the target completes the mission
  at once: chat "Mission accomplished!" (message 73), the quest item disappears, "Fornicationadremesh killed (name)" (a generated
  name), grade marks +13 in total, karma +1. No NPC hand-in in this case.
- Monster aggro in the dungeon: five monsters started moving when the player was 3.9, 4.0, 8.4, 11.1 and 12.3 m away
  (about 12 m maximum, same as upstream's value). The give-up distance is not measured.
- Leaving the dungeon: the client sends `ResetDungeonNumber` (player cmd 40) and reports the old city position; the server
  then sends the city objects. Same as a portal, the client announces the position.
- PvP in a city: an attack on another player inside a peaceful area is refused with system message 10302 ("You cannot attack
  the player, as you are in a peaceful area (city, shopping area)") and no damage. Traps in the dungeon take prana as well
  (-134 per tick, "The trap"). The emulator has no player-versus-player rules yet.
