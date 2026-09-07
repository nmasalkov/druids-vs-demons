# Druids vs Demons — Game Design Reference

*A complete description of what the game is and how it plays: every system, rule, number, and
process a player experiences, front to back. This document deliberately contains no technical or
implementation detail (no code, no engine terms, no architecture) — it describes behavior only, the
way a game designer's bible or an LLM briefed on "what does this game do" would need it. For how any
of this is actually built, see the `docs/*.md` files this was compiled from, or `CLAUDE.md`.*

*This file is generated/refreshed by the `generate-doc` skill from the project's technical docs plus
live balance data pulled from the game's data assets. Treat it as a snapshot — re-run the skill after
a mechanics or balance change instead of hand-editing this file.*

## Contents

1. [Elevator Pitch](#1-elevator-pitch)
2. [Session Structure](#2-session-structure)
3. [The Battle: Round & Turn Flow](#3-the-battle-round--turn-flow)
4. [The Slot Machine](#4-the-slot-machine)
5. [Creatures](#5-creatures)
6. [Combat Resolution](#6-combat-resolution)
7. [Nukes](#7-nukes)
8. [Spells](#8-spells)
9. [Heroes, Health & Win Condition](#9-heroes-health--win-condition)
10. [Reroll Energy](#10-reroll-energy)
11. [The Enemy AI](#11-the-enemy-ai)
12. [Campaign Structure](#12-campaign-structure)
13. [Pre-Battle Loadout](#13-pre-battle-loadout)
14. [Post-Battle Rewards](#14-post-battle-rewards)
15. [What Persists Across a Run](#15-what-persists-across-a-run)
16. [Balance Reference](#16-balance-reference)
17. [Current Scope & Known Gaps](#17-current-scope--known-gaps)

---

## 1. Elevator Pitch

Druids vs Demons is a 2D turn-based auto-battler. The player and an AI-controlled enemy each command
a Hero and up to three summoned creatures. Every turn, instead of picking an action from a menu, the
active side spins a 3-reel slot machine — the reels decide whether that turn summons/upgrades a
creature, or fires off an offensive Nuke, or casts a support Spell, and how strong it lands. Once
both sides have acted, whatever creatures are on the field fight automatically. Play continues,
round after round, until one Hero's health reaches zero.

Outside of any single battle, the player is playing a **run**: a fixed sequence of battles against
progressively tougher enemies. A persistent loadout (one Tank, one Archer, one Mage, three Nukes,
three Spells), a growing Max HP pool, and a reroll-energy currency all carry from fight to fight,
strengthened by reward cards earned after each win.

---

## 2. Session Structure

A **run** is the top-level container: start to finish, it's a straight line through a fixed list of
fights (5 today, ending in a "Final Boss"). There is no branching path to choose between — the player
advances through fights in a fixed order on a map screen shown between battles.

Each fight, independently, may be wrapped by up to two optional sub-phases:

- **A loadout-pick phase before it** — if present, the player reviews and can swap their equipped
  loadout before the fight begins (see [§13](#13-pre-battle-loadout)).
- **A reward phase after it**, on victory only — if present, the player receives guaranteed reroll
  energy and picks one of three reward cards before the run advances (see [§14](#14-post-battle-rewards)).

A fight with neither phase just plays straight through with no interruption. Losing a fight instead
retries that same fight from its start — same enemy, same starting stats for both sides — without
touching any run progress: no loadout re-pick, no reward, no rewind of fights already won.

Completing the last fight in the sequence ends the run. (Today this is a simple stopping point — see
[§17](#17-current-scope--known-gaps).)

A run's state — loadout, Max HP, reroll energy, unlocked items, claimed rewards, current fight —
autosaves at well-defined points (a new run starting, entering each new fight) so a session can be
closed and resumed without losing progress.

---

## 3. The Battle: Round & Turn Flow

A single fight plays out in **rounds**. Each round, both sides get a turn, always in the same order:

1. **Player's turn** — roll on the slot machine, resolve the result (summon/upgrade a creature, or
   fire a Nuke, or cast a Spell).
2. **Enemy's turn** — same thing, played out by the AI.
3. **Combat** — every creature currently on the field (both sides) fights automatically.
4. Round ends; loop back to step 1 for as long as both Heroes are still alive.

**Round 1 is a grace round for the player**: combat does *not* happen after the player's very first
turn, so the very first thing that happens in a fight is the player getting to summon or act with
zero risk. The enemy's turn on round 1 is otherwise normal — combat runs after it as usual. From round
2 onward, combat runs after *every* turn, both sides'.

**A turn can chain.** If a roll lands a perfect triple (all 3 reels show the same result), that
result is immediately locked in *and* the same side gets an automatic bonus follow-up turn — spin
again, act again — before the turn is considered over. This can in principle keep chaining off
another triple. See [§4](#4-the-slot-machine) for how triple odds are tuned.

**The battle ends the instant either Hero's HP hits 0** — this is checked continuously, not just
between turns, so a hit that kills a Hero mid-combat ends things immediately. Killing the enemy Hero
wins the fight regardless of how many enemy creatures are still alive; the reverse costs the player
the fight the same way. If both Heroes go down in the exact same instant, that counts as a loss for
the player (handled identically to any other defeat — see [§2](#2-session-structure)).

---

## 4. The Slot Machine

Every turn starts with a spin of a 3-reel slot machine.

**1. Choose a roll type.** Before spinning, the acting side picks one of three categories to roll
for (locked in once spinning starts):

- **Creature** — summon or empower a Tank / Archer / Mage.
- **Nuke** — an offensive burst effect.
- **Spell** — a support/utility effect.

**2. Spin.** Each of the 3 reels independently lands on one of that category's 3 currently-equipped
options (whichever creatures/Nukes/Spells the acting side has loaded — see [§13](#13-pre-battle-loadout)).

**3. Read the result.**
- All 3 reels different → no match. The turn can be finished as-is, or rerolled first (below).
- 2 reels match → a **level-2** result for that symbol.
- All 3 reels match (**a triple**) → a **level-3** result, and the roll finishes *immediately* — a
  triple can never be rerolled away, and it always grants the bonus follow-up turn described in
  [§3](#3-the-battle-round--turn-flow).

**4. Optionally reroll** any single reel (as many times as affordable) before finishing, chasing a
better match. The player pays Reroll Energy for this (see [§10](#10-reroll-energy)); the AI draws
from its own separate per-fight budget instead (see [§11](#11-the-enemy-ai)).

### What a match level does

- **Creature**: rolling **2-of-a-kind or 3-of-a-kind instantly promotes that creature to level 2 or
  level 3** — or spawns it fresh at that level if it isn't currently on the field. Rolling a
  **single, unmatched symbol** for a creature already on the field **heals it** instead (a flat
  amount depending on its *current* level — see [§16](#16-balance-reference)); rolling a single
  symbol for a creature *not* currently on the field spawns it fresh at level 1. A creature stolen
  from the enemy via Charm can only ever be healed this way, never promoted further.
- **Nuke / Spell**: the match level (1/2/3) scales that action's numbers — see
  [§7](#7-nukes)/[§8](#8-spells).

### Triple odds are tuned, not flat

Whenever two of the three reels already match, the odds the third reel completes the triple are not
a flat 1-in-3 — each side has its own hidden bias dial, roughly 0–200, where **100 is neutral** (a
genuine unbiased roll), **0 means a triple can never complete**, and **200 means it always does**.
This dial:

- **Resets at the start of every fresh turn**, generally biased to favor whichever side is currently
  losing on Hero HP (the worse off a side is, the likelier its near-triples complete).
- Is **forced to a fixed, deliberately-tuned value on the very first turn of the fight**, for both
  sides.
- Can optionally **cool down a little each time the same side chains a bonus turn off a triple**
  (a per-fight setting), so a hot streak within one turn can't snowball indefinitely.

Which physical reel ends up "the one that completes it" is randomized every roll, so there's never a
single reel that visibly always decides triples.

---

## 5. Creatures

Three roles, at most one native copy of each per side at a time (plus a small number of extra slots
for creatures stolen via Charm — see [§8](#8-spells)):

| Role | Style | Attacks/turn | Relative HP | Relative damage |
|---|---|---|---|---|
| **Tank** | Melee | 1 (fixed) | Highest | Lowest |
| **Archer** | Ranged | Multiple, grows with level | Middle | Middle |
| **Mage** | Ranged | 1 (fixed) | Lowest | Highest (by far) |

Every creature has **4 levels** of stats (damage / HP / attack count) built in — see
[§16](#16-balance-reference) for exact numbers. Damage, HP, and attack count are always read off the
creature's *current* level.

**Leveling always fully heals.** Whether a creature levels up via combat XP (below) or via a
matching slot-machine roll, it's immediately healed to its new level's full HP and any negative
status effect on it (see [§6](#6-combat-resolution)) is cleared. Leveling never demotes a creature.

**Combat XP.** Separately from slot-machine matches, creatures earn XP for participating in kills
during combat:
- Landing a killing blow on an enemy creature grants XP to every attacker that contributed to it.
- Landing *any* hit on the enemy Hero grants XP too — proportional to the damage dealt, whether or
  not the Hero survives the hit.
- Earned XP doesn't apply instantly — it visibly flies from the kill to the earning creature and
  only grants once it "arrives." A creature that dies before its earned XP arrives simply loses it;
  it never passes to a teammate.
- Crossing a creature's XP threshold promotes it to the next level (see the full heal/status-clear
  behavior above).

**Splash damage on creature death.** Whenever a friendly creature dies — from combat, a future
damage-over-time effect, anything — its own side's **Hero takes splash damage equal to 20% of that
creature's max HP** (rounded down). Overextending your board is a real cost, not a free trade.

---

## 6. Combat Resolution

After each turn (subject to the round-1 grace period), every creature on both boards fights in one
automatic pass:

**Targeting priority** (who gets attacked): a living **Shield** (see [§8](#8-spells)) is always hit
first — everything an attacker could target instead is ignored while a Shield is up. After that,
priority runs **Mage → Archer → Tank**: whichever squishy caster is still alive is targeted before
the sturdier Tank. Only once every creature on a side is gone does anything attack that side's Hero
directly.

**Attack order** (who acts first): the same priority — Mage, then Archer, then Tank.

**Multi-hit attackers** (an Archer with more than 1 attack at its level) fire each hit separately
within the same pass, **re-picking the current best target for every individual hit** — so a
multi-hit Archer can finish off one target with an early hit and redirect its remaining hits to the
next-priority target automatically, all in the same combat pass.

**Status effects that affect combat:**
- **Stunned** — a stunned creature skips its attack for the entire pass. A creature stays stunned
  until it's healed, promoted, or dies (there's no fixed countdown timer) — see Shock
  ([§7](#7-nukes)) for how stun is applied.
- **Battle Cry buff/debuff** — multiplies a creature's damage for the *rest of the battle* (not just
  one pass). If a debuff brings a creature's damage to zero, it also skips its attacks entirely for
  as long as the debuff holds.

**Tanks get special choreography**: if both sides' Tanks are targeting each other, they trade blows
in sequence rather than simultaneously. If more than one Tank on the same side ends up sharing a
target, they queue and take turns instead of all landing on top of each other at once. Every
non-Tank (ranged) attacker on a side fires simultaneously.

**Outcomes are locked in before any of it visibly plays out** — the entire pass's targets, hits, and
kills are decided up front, so a "watch it happen" playthrough and an instant, no-animation
resolution of the exact same pass always produce identical results; they only differ in *how* the
same outcome gets shown.

---

## 7. Nukes

One-shot offensive effects, always aimed automatically at priority targets on the enemy's side (no
manual targeting). Priority order for a Nuke's targets is: **Shield (unless the Nuke ignores
shields) → Mage → Tank → Archer → Hero**, always the *lowest-HP-remaining-first-exhausted* pool
within a class before moving to the next.

- **Fire Magic** — pours a single flat pool of damage into priority targets one after another until
  the pool runs out. At low match-levels it may only tap the Shield; at higher levels it can punch
  through the Shield and multiple creatures in one cast.
- **Starfall** — strikes the enemy Hero directly, **bypassing their Shield entirely** — the one Nuke
  that ignores shields by design. Its damage doesn't get "spent" on anything else; it always lands
  fully on the Hero.
- **Shock** — deals **no damage** at all. Instead it **stuns** a number of priority targets equal to
  its match level (level 1 stuns 1 target, level 2 stuns 2, level 3 stuns all 3), making each one
  skip attacks until cured (healed, promoted, or killed).

Every Nuke's exact numbers scale with the roll's match level (1/2/3) — see
[§16](#16-balance-reference).

---

## 8. Spells

Support/utility effects, generally affecting a side's own board or defenses rather than dealing
direct burst damage to the enemy Hero:

- **Shield** — raises (or strengthens) a protective barrier in front of your own Hero. While a
  Shield is up, it intercepts *every* attack aimed at that side before anything else can be hit
  (see [§6](#6-combat-resolution)) and grants no XP for killing it.
  - No Shield yet → spawns a fresh one at the rolled level's starting HP.
  - Existing Shield, rolled a **higher** level → instantly promotes it to that level and fully
    refills its HP.
  - Existing Shield, rolled an **equal or lower** level → just heals it for a flat amount instead.
- **Charm** *(sometimes called "hack")* — a chance to **steal one living enemy creature** to your
  own side for the rest of the battle.
  - Only creature roles you currently have a free slot for are eligible targets.
  - Success chance scales with match level **and** with how many enemy creatures are currently
    alive (more live targets = better odds, capped at 100%). The success/failure roll happens once,
    up front — a failed attempt still consumes the turn but changes nothing.
  - Among eligible targets, the match level picks *which* one: level 1 picks the weakest eligible
    target, level 3 the strongest, level 2 in between.
  - A charmed creature fights for its new side until it dies, gets healed by matching rolls (but
    never promoted further — see [§5](#5-creatures)), or gets **stolen right back** by its original
    owner casting Charm again.
- **Battle Cry** — instantly buffs every creature *currently on your own board* and debuffs every
  creature *currently on the enemy's board*, both by a flat damage multiplier that scales with match
  level and **lasts for the rest of the battle**, not just the current combat pass — creatures
  summoned after the cast still count toward the buff. At the strongest debuff tier, affected enemy
  creatures' damage is reduced to nothing, meaning they skip their attacks entirely for the rest of
  the fight.

Every Spell's exact numbers scale with the roll's match level — see [§16](#16-balance-reference).

---

## 9. Heroes, Health & Win Condition

Each side fields exactly one Hero with a fixed Max HP pool for that fight:

- The **player's** Hero's Max HP is the run's persistent base value plus every Vitality reward
  claimed so far (see [§14](#14-post-battle-rewards)/[§15](#15-what-persists-across-a-run)).
- The **enemy's** Hero's Max HP is set individually per fight (tougher fights field tougher Heroes).

A Hero is only ever attacked once its side's Shield (if any) and every creature it controls are gone
— see targeting priority in [§6](#6-combat-resolution). The battle ends the instant either Hero's HP
reaches 0 (see [§3](#3-the-battle-round--turn-flow)) — creatures still standing on the losing side
don't prevent or delay this.

---

## 10. Reroll Energy

The player's currency for rerolling slot-machine reels.

- **The energy pool is a single persistent number that carries across the entire run** (see
  [§15](#15-what-persists-across-a-run)) — spending it in one fight leaves less available in the
  next, unless refilled by a reward.
- **Reroll cost** starts at a fixed base price and **doubles with every reroll made within the same
  turn**, resetting back to the base price the instant that turn's roll finishes. Chasing one
  specific result deep into a single turn gets exponentially more expensive; spreading rerolls
  across different turns doesn't.
- The reroll option automatically becomes unavailable once the player can't afford the current cost.
- **Restarting the current fight** (after a loss, or via the pause menu) refunds energy back to
  exactly what it was when this fight attempt began — so retries never cost more than the fight
  itself. **Advancing to a genuinely new fight**, by contrast, carries forward whatever was actually
  spent.

The enemy AI never touches this system — it rerolls from its own completely separate, fight-long
budget instead (see [§11](#11-the-enemy-ai)).

---

## 11. The Enemy AI

The enemy plays by the exact same rules as the player — same slot machine, same Nukes/Spells, same
combat — but every decision is made automatically. Two per-fight dials govern how well it plays:

- **Stupidity chance** — the odds the AI *doesn't* take its own objectively best move that turn.
- **Critical-failure chance** — rolled only once stupidity has already triggered; on success, the AI
  picks something **fully random** instead of merely settling for its next-best option.

So a "weaker" fight isn't uniformly weak — it plays its best move most of the time, occasionally
settles for second-best, and rarely does something outright random. A handful of decisions are
**always played at full strength**, never subject to these dials:
- Summoning something on the very first turn if nothing is out yet.
- Never trying to summon into an already-full board.
- **The single highest-priority decision of all**: if any of the AI's own creatures is currently
  stunned, curing it (by rolling that creature's type again, which heals and cures the stun at once
  — see [§5](#5-creatures)) always wins out over any other summon decision, no exceptions.

**What the AI decides each turn:**

- **Whether to summon a creature, and which one.** Priority: cure a stunned creature first (above),
  then fill an empty Tank slot, then react to whichever of the *player's* units/Hero is critically
  low on HP (favoring an Archer pick if a player creature is low, a Mage pick if the player's Hero
  itself is low), falling back to whichever role it doesn't have yet if nothing else applies.
- **Whether to cast a Nuke/Spell instead, and which one.** Every Nuke/Spell the AI has equipped gets
  scored against current board state (HP percentages, who has a Shield, creature counts, etc.), and
  the AI leans toward whichever scores highest.
- **How much to reroll.** The AI has both a **fight-long reroll budget** (below) and a per-turn cap
  that flexes with how promising the current roll already looks: it rerolls aggressively when
  chasing a 2-of-a-kind toward a triple, barely rerolls at all once it already has the single match
  it wanted, and rerolls *more* than its normal cap — spending down its budget faster — whenever
  either Hero is critically low on HP.
- **No repeating a just-tripled action.** If the AI's own roll just triggered a bonus turn off a
  triple, it's specifically barred from choosing that *exact same* action again on the bonus turn.

**The fight-long reroll budget is the real limiter**, not any per-turn cap: each fight gives the AI a
fixed number of total rerolls to spend across its *entire* fight, never refilled except by a full
restart. Once it's spent, the AI simply stops rerolling for the rest of that fight.

Fights generally tune the AI to be clumsier and more reroll-starved early in the run, and sharper and
better-resourced the deeper the run goes — see the fight table in [§16](#16-balance-reference).

---

## 12. Campaign Structure

A run's fights are laid out as points on a linear map, walked in a fixed order (no player choice of
which fight comes next yet). For each point/fight, independently:

- **If it has a loadout-pick phase**, arriving at it shows the loadout picker in place before the
  fight starts (see [§13](#13-pre-battle-loadout)); confirming the picker (or if there is no such
  phase) transitions straight into the battle.
- **On victory, if the fight has a reward phase**, the reward screen appears immediately, right on
  top of the victory moment; only once a reward is claimed does the run actually advance to the next
  point on the map (see [§14](#14-post-battle-rewards)). If there's no reward phase, the run advances
  immediately.
- **On defeat**, the same fight reloads from scratch — the run doesn't move, nothing is lost, no
  phase needs redoing.

Each fight independently defines its own enemy Hero (portrait and Max HP) and, once specifically
authored, its own trio of enemy creatures — a fight without a custom roster simply mirrors whatever
creatures the *player* currently has equipped for that fight (an effective "mirror match"). Each
fight also independently tunes the enemy AI's stupidity/critical-failure/reroll-budget dials (see
[§11](#11-the-enemy-ai)) and can optionally dampen a runaway lucky-triple streak (see
[§4](#4-the-slot-machine)).

Completing the last fight in the sequence ends the run.

---

## 13. Pre-Battle Loadout

Before certain fights, the player can freely review and adjust their **9 equipped slots** — 1 Tank,
1 Archer, 1 Mage, 3 Nuke slots, 3 Spell slots — against everything they've unlocked so far via
rewards:

- **Creature slots** only accept a matching role (only Tanks fit the Tank slot, etc.).
- **Nuke/Spell slots** have no further restriction — any unlocked Nuke fits any of the 3 Nuke slots,
  and likewise for Spells.
- A side-by-side comparison shows the currently-equipped item next to a candidate before the player
  commits to a swap, so the tradeoff is visible up front.
- **Nothing is written to the run until the picker is explicitly confirmed** — browsing and
  comparing candidates freely, without swapping, costs nothing and can be undone by simply not
  confirming that change.

---

## 14. Post-Battle Rewards

A fight that grants a reward always does two things on the victory screen:

**1. A guaranteed, flat chunk of reroll energy** is granted the instant the screen opens — no choice
involved, it always applies in full.

**2. Exactly 3 randomly drawn reward cards are offered, and the player claims one.** Reward types:

| Reward | Effect | Repeatable? |
|---|---|---|
| **Vitality** | Permanent +Max HP for the rest of the run | Yes — stacks with every claim, no cap |
| **Energy Cell** | An immediate, one-time chunk of bonus reroll energy (not capped — always fully applies) | Yes |
| **Unlock [Creature variant]** | Permanently unlocks an upgraded creature variant for the Loadout picker | Once per specific variant |
| **Boost [Creature/Nuke/Spell]** | A permanent percentage buff to one specific action's key numbers (damage & HP for a creature, damage for a Nuke, buff strength for Battle Cry, success chance for Charm, HP for Shield) | Once per specific target |

**Draw rule**: the first of the 3 cards is always either a Vitality or an Energy Cell card. The other
two lean toward offering one creature-unlock and one boost (when both still have unclaimed options
available), falling back to more Vitality/Energy cards once every unlock and every boost has already
been claimed. A reward that's already maxed out (a unique unlock/boost already claimed) never
reappears in a future draw.

---

## 15. What Persists Across a Run

The following carry forward from fight to fight for the whole run, until the run itself restarts:

- **Max HP** — base value plus every claimed Vitality reward.
- **Reroll energy** — spent by rerolling, replenished by guaranteed post-battle grants and Energy
  Cell rewards (uncapped).
- **The 9 equipped loadout slots** (creature/Nuke/Spell picks).
- **Everything unlocked so far** — which creature variants, Nukes, and Spells are available to equip
  via the Loadout picker.
- **Every claimed reward** — so Boost/unlock/Vitality stacks are never lost, and a unique reward
  never reappears once claimed.
- **Which fight in the sequence the player is currently on.**

A same-fight restart reverts only reroll energy (back to what it was when that attempt began);
everything else above is untouched by a restart.

---

## 16. Balance Reference

### Starting loadout

The player begins every run equipped with:

- **Creatures**: Golem (Tank), Bubka (Archer), Dragon (Mage)
- **Nukes**: Fire Magic, Starfall, Shock (all 3 that currently exist in normal play)
- **Spells**: Battle Cry, Charm, Shield (all 3 that currently exist in normal play)

Starting run stats: **100 Max HP**, **50 / 50 reroll energy**, base reroll cost **2** (doubling per
reroll within a turn, per [§10](#10-reroll-energy)).

### Creature stats by level (damage / HP / attacks per turn)

All creatures share the same XP curve regardless of role:
- **XP required to reach each level** (cumulative): L1 = 0, L2 = 120, L3 = 320, L4 = 600.
- **XP granted for killing this creature** (by *its own* level at death): L1 = 30, L2 = 60,
  L3 = 120, L4 = 240.
- **Heal amount when rolled without a match** (by its *current* level): L1 = 3, L2 = 7, L3 = 10,
  L4 = 24.
- Killing this creature deals its side's own Hero splash damage equal to 20% of its max HP at that
  level (see [§5](#5-creatures)).

| Role | Name | Where found | L1 (dmg/hp/atk) | L2 | L3 | L4 |
|---|---|---|---|---|---|---|
| Tank | **Golem** | Player starting loadout | 4 / 36 / 1 | 6 / 46 / 1 | 8 / 56 / 1 | 10 / 66 / 1 |
| Tank | Cyclop | Fight 1 enemy roster (identical stats to Golem) | 4 / 36 / 1 | 6 / 46 / 1 | 8 / 56 / 1 | 10 / 66 / 1 |
| Tank | Skeleton | Fight 2 enemy roster | 4 / 45 / 1 | 6 / 55 / 1 | 8 / 70 / 1 | 10 / 80 / 1 |
| Tank | OrkTank | Fight 3 enemy roster | 5 / 45 / 1 | 7 / 55 / 1 | 10 / 70 / 1 | 12 / 80 / 1 |
| Archer | **Bubka** | Player starting loadout | 3 / 28 / 2 | 3 / 34 / 3 | 5 / 42 / 3 | 6 / 50 / 4 |
| Archer | Demon | Fight 1–2 enemy roster (identical stats to Bubka) | 3 / 28 / 2 | 3 / 34 / 3 | 5 / 42 / 3 | 6 / 50 / 4 |
| Archer | Kodo | Fight 3 enemy roster | 4 / 28 / 2 | 4 / 34 / 3 | 4 / 42 / 4 | 5 / 50 / 5 |
| Mage | **Dragon** | Player starting loadout | 9 / 24 / 1 | 15 / 28 / 1 | 22 / 32 / 1 | 34 / 42 / 1 |
| Mage | Bat | Fight 1–2 enemy roster (identical stats to Dragon) | 9 / 24 / 1 | 15 / 28 / 1 | 22 / 32 / 1 | 34 / 42 / 1 |
| Mage | OrkMage | Fight 3 enemy roster | 11 / 24 / 1 | 17 / 28 / 1 | 24 / 32 / 1 | 38 / 42 / 1 |

Three additional upgraded variants of the player's own starting three (**Bubka Big**, **Golem Big**,
**Dragon Big** — noticeably stronger versions of Bubka/Golem/Dragon) exist as Loadout options, but are
locked behind their own reward-card unlocks (see [§14](#14-post-battle-rewards)) rather than owned
from the start.

### Nukes by match level

| Nuke | L1 | L2 | L3 | Behavior |
|---|---|---|---|---|
| **Fire Magic** | 13 dmg | 20 dmg | 60 dmg | Flat damage pool, spent through priority targets (Shield → Mage → Tank → Archer → Hero) until exhausted |
| **Starfall** | 5 dmg | 11 dmg | 17 dmg | Hits the enemy Hero directly, always bypassing their Shield |
| **Shock** | Stuns 1 target | Stuns 2 targets | Stuns 3 targets | Deals no damage; stuns priority targets (Mage → Tank → Archer → Hero) until they're cured |

### Spells by match level

| Spell | L1 | L2 | L3 | Behavior |
|---|---|---|---|---|
| **Shield** | 15 HP (7 heal if refreshing) | 27 HP (13 heal) | 56 HP (27 heal) | Spawns/promotes/heals your own Shield |
| **Charm** | 12% chance × alive enemy creatures | 21% × alive enemy creatures | 35% × alive enemy creatures | Steals weakest (L1) / median (L2) / strongest (L3) eligible enemy creature; chance capped at 100% |
| **Battle Cry** | +24% own dmg / −60% enemy dmg | +40% own dmg / −90% enemy dmg | +80% own dmg / enemy dmg reduced to 0 (they skip attacks) | Buffs your board, debuffs enemy board, for the rest of the battle |

### Fight-by-fight progression

| # | Fight | Enemy Max HP | Enemy roster | Loadout pick before? | Reward after? | Reward energy | AI stupidity | AI crit-failure | AI reroll budget |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Tutorial | 50 | Demon / Cyclop / Bat | No | Yes | +20 | 50% | 20% | 6 |
| 2 | Skeleton | 80 | Demon / Skeleton / Bat | Yes | Yes | +20 | 35% | 15% | 10 |
| 3 | Orks | 100 | Kodo / OrkTank / OrkMage | Yes | Yes | +40 | 20% | 15% | 10 |
| 4 | Elite | *not yet authored — mirrors the player's own roster* | Yes | Yes | +50 | 20% | 15% | 10 |
| 5 | Final Boss | *not yet authored — mirrors the player's own roster* | Yes | No | — | 20% | 15% | 10 |

The player's Hero starts every fight at **100 Max HP** (plus any claimed Vitality rewards). Lower
`stupidity`/`critical-failure` numbers and a bigger reroll budget both mean a *sharper* enemy — the
progression above tunes the AI to be its clumsiest, most reroll-starved self on the Tutorial fight
and its sharpest from Fight 3 onward.

### Reward cards

| Card | Type | Effect | Claimable |
|---|---|---|---|
| Vitality | Status | +15 Max HP | Repeatedly, no cap |
| Energy Cell | Status | +50 reroll energy, instantly | Repeatedly |
| Unlock Bubka Big / Golem Big / Dragon Big | Creature | Adds the upgraded variant to the Loadout pool | Once each |
| Boost Bubka / Golem / Dragon | Boost | +20% damage & HP to that creature | Once each |
| Boost Fire Magic / Starfall | Boost | +20% damage to that Nuke | Once each |
| Boost Shield | Boost | +20% HP to Shield | Once |
| Boost Charm | Boost | +20% success chance to Charm | Once |
| Boost Battle Cry | Boost | +20% to Battle Cry's buff strength (debuff side unaffected) | Once |
| Boost Shock | Boost | Currently has no effect — Shock has no boostable stat yet | Once |

A fourth Nuke, **Fireball** (flat damage, no shield-ignore, roughly comparable in strength to a
souped-up Fire Magic), exists in the game's data but currently has no reward or unlock path that
makes it reachable in play.

---

## 17. Current Scope & Known Gaps

Things that exist in the game's data/design but aren't fully live yet, or are intentionally
placeholder — worth knowing before treating this document as describing a 100%-finished game:

- **Only 5 fights are authored**, and the last two (Elite, Final Boss) don't have their own enemy
  creature roster yet — they currently mirror whatever the player has equipped (an effective mirror
  match) rather than fielding a bespoke enemy team.
- **The campaign is a straight line, not a branching path** — there's no player choice of which
  fight to take next, only a fixed sequence.
- **Completing the run has no dedicated ending screen yet** — it's currently a simple, unmistakable
  stop rather than a victory celebration/summary.
- **Boost Shock is inert** — Shock doesn't currently have a numeric stat that a Boost card could
  improve, so its card exists but does nothing yet.
- **Fireball (a 4th Nuke)** exists in the game's data but has no unlock/reward path yet, so it can't
  currently be obtained in a normal run.
- **A run offers only one difficulty path** — enemy toughness is fixed per fight, not adjustable by
  the player.
- **Energy has no hard cap** — nothing currently limits how high the reroll-energy pool can climb;
  every design note describing a possible future "capacity" cap is unenforced today.

---

*Compiled from `docs/GameLoop.md`, `docs/SlotMachine.md`, `docs/Battle.md`,
`docs/ActionsAndSpells.md`, `docs/Experience.md`, `docs/Energy.md`, `docs/AI.md`, `docs/Campaign.md`,
`docs/Encounters.md`, `docs/Loadout.md`, `docs/Rewards.md`, `docs/G.md`, and live balance data read
from the game's creature/Nuke/Spell/fight/reward data assets.*
