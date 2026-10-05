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
12. [Campaign Structure & the Map](#12-campaign-structure--the-map)
13. [Multi-Phase Boss Fights](#13-multi-phase-boss-fights)
14. [Pre-Battle Loadout](#14-pre-battle-loadout)
15. [Post-Battle Rewards](#15-post-battle-rewards)
16. [What Persists Across a Run](#16-what-persists-across-a-run)
17. [Balance Reference](#17-balance-reference)
18. [Current Scope & Known Gaps](#18-current-scope--known-gaps)

---

## 1. Elevator Pitch

Druids vs Demons is a 2D turn-based auto-battler. The player and an AI-controlled enemy each command
a Hero and up to three summoned creatures. Every turn, instead of picking an action from a menu, the
active side spins a 3-reel slot machine — the reels decide whether that turn summons/upgrades a
creature, fires an offensive Nuke, or casts a support Spell, and how strong it lands. Once a side has
acted, every creature on the field fights automatically. Play continues, round after round, until
one Hero's health reaches zero.

Outside of any single battle, the player is playing a **run**: a fixed sequence of five battles
against progressively tougher, themed enemy rosters, ending in a two-phase final boss. A persistent
loadout (one Tank, one Archer, one Mage, three Nukes, three Spells), a growing Max HP pool, and a
reroll-energy currency all carry from fight to fight, strengthened by reward cards earned after each
win.

---

## 2. Session Structure

A **run** is the top-level container: a straight line through a fixed list of 5 fights, the last
being a two-phase Final Boss. There is no branching route — fights unlock in a fixed order on a map
screen shown between battles — but the player chooses *when* to fight: the newest unlocked fight, or
a **replay** of any fight already beaten.

Each fight may be wrapped by up to two optional sub-phases:

- **A loadout-pick phase before it** — the player reviews and can swap their equipped loadout before
  the fight begins (see [§14](#14-pre-battle-loadout)). Every fight except the first has one.
- **A reward phase after it**, on victory only — the player receives guaranteed reroll energy and
  picks one of three reward cards (see [§15](#15-post-battle-rewards)). Every fight except the final
  boss has one.

**Winning** the newest fight (and confirming any reward) advances the run to the next fight and
returns to the map, which reveals it. **Winning a replay** grants the full reward pick again but never
advances progress; it returns the player to the map.

**Losing** does not restart automatically. After a short pause a locked menu appears with two choices:
**Restart** (retry the same fight from its beginning — same enemy, same starting stats for both sides)
or **Go to map** (leave the fight and pick again from the map). Either way, nothing about the run
changes: no loadout re-pick, no reward, no rewind of fights already won, and any reroll energy spent
*or earned* during the failed attempt is reverted. Retrying costs nothing but time.

Completing the last fight ends the run (today a simple stop — see [§18](#18-current-scope--known-gaps)).

A run's state — loadout, Max HP, reroll energy, unlocked items, claimed rewards, furthest fight
reached — autosaves at well-defined points (a new run starting, advancing to each new fight, the
reward energy being granted, claiming a reward card), so a session can be closed and resumed without
losing progress. Resuming lands on the map.

---

## 3. The Battle: Round & Turn Flow

A battle opens with a short intro beat, then runs as a sequence of **rounds**. One round is the
player's turn followed by the enemy's turn.

Each side's turn, in order:

1. **Roll** — the active side spins the slot machine and settles on three results.
2. **Resolve** — depending on the action type rolled, the turn summons/upgrades creatures, fires
   Nukes, or casts Spells.
3. **Battle** — every creature on both sides attacks automatically (see [§6](#6-combat-resolution)).
4. **Cleanup** — dead bodies are removed, Battle Cry effects expire, and XP gems fly to the creatures
   that earned them.

**Round 1 is special: the player's turn is not followed by a battle phase.** The player summons their
opening creatures and isn't immediately attacked for it. The enemy's turn always ends in a battle
phase, including in round 1.

**Landing a triple grants a bonus turn.** If all three reels land the same result, the acting side
immediately rolls and resolves again before play passes on. This can chain. A bonus turn does *not*
advance the round counter. **A player triple also pays out 1 reroll energy** (see
[§10](#10-reroll-energy)); the enemy gets no such payout.

The battle ends the instant either Hero's health reaches zero — see [§9](#9-heroes-health--win-condition).
The one exception is a boss with a second phase, which rises again instead (see [§13](#13-multi-phase-boss-fights)).

Escape pauses the battle at any time, genuinely freezing everything (animations, timers, pending
effects). The pause menu offers **Restart** (retry this fight) and **Go to map** (abandon it, refunding
this attempt's energy).

---

## 4. The Slot Machine

The heart of the game. Three reels, and — before spinning — a choice of which of three **action
types** to roll:

- **Creature** — the reels show the side's Tank, Archer, and Mage.
- **Nuke** — the reels show the side's three equipped Nukes.
- **Spell** — the reels show the side's three equipped Spells.

The action type can only be switched before the first spin of a turn. Once the reels are spinning,
the choice is locked.

### How a roll resolves into strength

After the reels land, identical results are grouped. Each distinct result produces one action, and
**how many reels showed it becomes that action's strength level** (1, 2, or 3):

- **One reel** → level 1.
- **Two reels** → level 2, noticeably stronger.
- **Three reels (a triple)** → level 3, the strongest version — *and* a bonus turn (plus 1 reroll
  energy, if it's the player).

A mixed roll fires several actions at once: Fire Magic, Fire Magic, Starfall resolves a level-2 Fire
Magic *and* a level-1 Starfall. For Creature rolls, the level is the level the creature is summoned or
promoted **to** — rolling two Tanks puts a level-2 Tank on the board directly.

### Rerolling

Once all three reels land — unless it's already a triple — the player may reroll individual reels,
paying energy each time (see [§10](#10-reroll-energy)), then press Finish to lock the result in. A
triple auto-finishes with no reroll opportunity, and a reroll that completes a triple also
auto-finishes immediately.

### Triple odds are tuned, not flat

The odds of a triple are deliberately rigged, per side, and shift over the course of a fight. Two
dials govern it, both on a **0–200 scale where 100 is neutral**. The scale passes through three fixed
points: 0 → never, 100 → a genuine 1-in-3, 200 → always (linear between them).

- **The clean-triple dial** is checked once per fresh spin, before the reels are decided at all. On
  success the spin lands an instant triple. On failure the spin is forced to *not* be a triple, so
  every fresh-spin triple comes from this deliberate check.
- **The dirty-triple dial** governs the other route: when two reels already match and the third is
  being decided (in practice, a reroll), it sets the odds that third reel completes the triple.

Which physical reel ends up "completing" a triple is randomized every roll, so no reel is ever visibly
the decider. Both dials are recomputed at the start of every fresh turn, per side, per fight.

### Comeback assistance

At the start of each side's turn, both dials get the same **comeback adjustment** — the mechanic that
quietly helps whoever is losing. Each fight defines a ladder of comeback rules per side, each rule
with three parts:

1. **An HP threshold** — "this side's Hero is at or below X% health."
2. **A firepower-disadvantage threshold** — "the opponent out-guns this side by at least N points"
   (0 = ignore firepower, pure health rule).
3. **A triple adjustment** — the amount added to both dials.

A rule fires if **either** condition is met. Of all rules that fire, **the largest adjustment wins**
(they never stack). Adjustments can be negative, which is how a side that's comfortably ahead is
*punished*: the top rung of every ladder applies at any health with a negative value, and only stops
winning once a lower rung fires. With the standard ladder shape used by every fight:

| This side's health | No firepower disadvantage | Out-gunned by 30+ | by 40+ | by 50+ |
|---|---|---|---|---|
| 100% – 61% | punish (rung 1) | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 60% – 26% | rung 2 (0 in most fights) | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 25% – 16% | tier-1 bonus | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 15% – 11% | tier-2 bonus | tier-2 bonus | tier-2 bonus | tier-3 bonus |
| 10% – 0% | tier-3 bonus | tier-3 bonus | tier-3 bonus | tier-3 bonus |

Per-fight values are in [§17](#17-balance-reference).

**"Firepower" means expected total damage per battle phase**: for each side, the sum over all its
creatures of damage-per-hit × hits-per-turn (including reward boosts and any active Battle Cry),
with stunned creatures counting as zero. **Critical strikes count at their average value** (20% for
+50% counts as +10%). A creature that never attacks (Bulba) counts at a nominal value equal to a
standard tank's damage. **The opposing barrier is then subtracted**: a Shield standing in a side's way
absorbs damage first, so its health is deducted from that side's firepower, floored at zero.
"Advantage" is the difference between the two sides' effective firepower.

Shield-breaking (×1.5 vs barriers) is *not* counted, since no target is known when this is estimated —
a shield-breaking roster reads slightly weaker than it really is.

**A comeback bonus decays as it works.** Each time a side lands a triple and takes a bonus turn, a
*positive* adjustment is divided by 3 (every fight), and both dials drop by the same amount — 50 → 16
→ 5 → 1. A *negative* adjustment never decays within the turn.

**A comeback bonus that worked is weaker next time.** If a side started a turn with positive comeback
help and landed a triple during it, its *next* turn's bonus drops one rung down its ladder (e.g. +50 →
+37; the smallest positive rung → 0, never into a penalty). Next turn only, no stacking; it re-arms
only if that weakened turn triples again. Applies to both sides.

**Round 1 ignores comeback assistance entirely** — both dials are forced to fixed per-fight opening
values for both sides' first turns.

### Further tuning levers

- **Streak stabilization** — each bonus turn chained off a triple can push a side's dials back down
  by a per-fight amount, applied after comeback decay, reset on the next fresh turn. Today only the
  enemy's clean-triple dial uses this (−25 or −50 per bonus turn, depending on the fight).
- **Reroll pity ("ludo progress")** — during a side's *first* roll phase of a turn only, every reroll
  nudges the dirty-triple dial up by a per-fight amount (tunable per round number), so fishing for a
  triple gets progressively likelier. Undone the moment that roll phase ends, and off for the rest of
  a turn once that side has already tripled. Only the Tutorial uses it (+50 per reroll, rounds 1–3).

---

## 5. Creatures

Each side fields up to **three creatures of its own** — one Tank, one Archer, one Mage — in three
dedicated slots. There are additionally two "stolen" slots per class, which only fill via the Charm
spell (see [§8](#8-spells)).

The three archetypes:

- **Tank** — highest health, low damage, one attack, and the only melee attacker: it runs at its
  target, strikes, and leaps back.
- **Archer** — moderate health and low damage per hit, but **multiple attacks per turn**, re-picking
  its target after each hit, so it can finish off a weakened target and move on.
- **Mage** — lowest health, highest single-hit damage, one attack per turn.

### Creature traits

On top of archetype stats, a creature can carry traits (exact order of application in
[§6](#6-combat-resolution)):

- **Shield-breaking** — 1.5× damage against barriers only. The ork roster.
- **Critical strike** — a per-hit chance to deal bonus damage (e.g. 20% chance of +50%). Ghost roster
  and the final boss's second-phase roster (20% / +50%), and the player-unlockable Lizard (35% / +100%).
- **Taunt** — enemy creature attacks target this creature before any other creature (still behind a
  standing barrier). Nukes ignore taunt. Bulba only.

### Special creatures (player unlocks)

- **Bulba (counter-tank)** — never attacks on its own. It taunts, and **every regular creature hit
  that lands on it is answered with a ranged counterattack** for a fixed fraction of that hit's final
  damage (50/60/70/80% by level). The counter goes at the barrier guarding the attacker's side if one
  still stands after the battle's planned hits, otherwise at the attacker itself. Details:
  - A shocked Bulba doesn't counter.
  - A counter is skipped if its target would already be dead by the end of the battle anyway.
  - The killing blow on Bulba is still countered.
  - Counter damage is flat: reward boosts and Battle Cry on Bulba's side, crits, and shield-breaking
    never change it. A Battle Cry on the *attacker's* side does make it bigger, since the hit itself is
    bigger.
  - Nukes never trigger counters.
  - A counter kill grants Bulba the normal kill XP; a counter on a barrier grants none.
- **Healing Shroom (healer archer)** — frail archer that **shoots twice per battle**. First, a support
  volley *before* any attack lands: each of its shots heals one random wounded ally (another creature
  or its Hero — never itself, never a barrier) for 75% of its current per-hit damage (reward boost and
  Battle Cry included, so Shock/Energy Drain cancel it). Repeats on the same ally are allowed; a shot
  with no wounded ally heals nothing. These heals don't cure Shock. Then the regular battle plays out,
  in which it attacks like any archer.
- **Lizard (glass-cannon mage)** — ~70% of a Dragon's health, ~30% more damage, 35% chance to crit for
  double damage.

### Summoning and promotion

Rolling a creature type either summons it (if its slot is empty) or, if one is already there,
**promotes it to the rolled level** if that's higher — or **heals it** by a fixed amount if it isn't.
Promotion fully refills health at the new level's maximum. Creatures of the same class sitting in your
stolen slots are healed (never promoted) by the same roll.

Both promotion and healing **clear a creature's stun**, so re-rolling your own creature type is a
legitimate way to un-stun it.

### Levels and experience

Creatures level from 1 to 4, either by being rolled at a higher level or by earning experience in
combat. Damage, health, and attack count all scale with level. Leveling up also fully heals.

Experience is earned during battle and paid out afterward as gems that fly from the hit location to
the creature that earned them:

- **Killing a creature** grants XP based on the *victim's* level, once per attacker that hit it in the
  killing battle.
- **Hitting a Hero** grants 10× the damage dealt, on every hit (crits included).
- **Hitting a barrier grants nothing.**

If a creature dies before its gem lands, that XP is lost.

### Death splashes onto your own Hero

**Whenever a creature dies in play, its own side's Hero takes damage equal to 20% of that creature's
maximum health, rounded down.** Both sides, any cause of death, regardless of overkill. (The scripted
board wipe during a boss's phase change is the one exception — see [§13](#13-multi-phase-boss-fights).)

---

## 6. Combat Resolution

After a side's turn resolves, every creature on the field attacks, both sides at once. The whole
exchange — every target, every crit roll, every heal and counter — is decided up front against a
snapshot of everyone's health, then played out. The animation is just presentation.

### Targeting priority

Every attacker picks its target by the same rule, re-evaluated for each hit:

1. **The opposing barrier**, if one is up — always first.
2. Otherwise a **taunting** creature (Bulba), if any.
3. Otherwise the highest-priority living opposing creature, in the fixed order **Mage → Archer → Tank**.
4. Otherwise the opposing **Hero**.

A Hero is never attacked while any of its creatures still live. The Mage → Archer → Tank order also
decides which attacker acts first.

### Order of events within a battle

1. **Support volleys** — any Healing Shroom heals land first.
2. **Regular attacks** — both sides.
3. **Counterattacks** — Bulba's answers to the hits it took, played as each triggering hit lands.

### Per-attacker rules

- A **stunned** creature skips its turn entirely.
- An attacker fires its full attack count, re-picking a target for each hit.
- **Each hit's damage is built in a fixed order:**
  1. The creature's level damage, × the player's class reward boost if any.
  2. × any active **Battle Cry** buff or debuff. If a debuff reduces damage to zero, the attacker skips
     its turn (nothing below can bring it back).
  3. × any **target-dependent traits** — today only shield-breaking (×1.5 against a barrier).
  4. × the **critical strike**, always last, rolled separately for every hit.

  Example: a 3-damage hit under a doubled Battle Cry, shield-breaking against a barrier, with a +50%
  crit lands for 3 × 2 × 1.5 × 1.5 = 13.5.

### Choreography

Ranged attackers fire simultaneously from their slots. Tanks physically travel to their target:

- **Two tanks targeting each other** duel: one runs in, the other launches as the first arrives.
- **Multiple tanks sharing a target** queue, each waiting for the previous one to leap back.
- **Barrier first, then the Hero behind it**: if one tank breaks a barrier and another goes for the
  Hero behind it, the second visibly waits for the break.

A target killed mid-attack defers its death animation until the attack finishes.

**A critical hit gives a small screen shake** the moment it lands. Normal hits have no generic hit
feedback; the final boss plays a hurt animation on every hit. Ballistic projectiles (thrown axes,
rocks) arc to their target and can tumble in flight — purely visual.

---

## 7. Nukes

Nukes are the offensive action type. A side equips three. Rolling them fires them at the rolled
levels. Nukes never critically strike and never trigger counterattacks.

- **Fire Magic** — one damage pool spread down a priority list: the barrier first, then every living
  Mage, then Tanks, then Archers, then the Hero. Each target absorbs up to its remaining health and the
  rest moves on until the pool is spent.
- **Starfall** — **ignores the barrier entirely** and strikes **every** living enemy creature *and*
  the enemy Hero for the same flat damage. Low per-target damage, but unblockable and board-wide.
- **Shock** — stuns instead of damaging: one bolt per level, each stunning one creature, chosen
  Mage → Tank → Archer, **preferring creatures that aren't already stunned**. A stunned creature skips
  its combat turn until it's healed, promoted, or killed. Heroes can't be stunned. **A barrier stops
  Shock completely** — while the target side has a barrier up, the bolts fizzle harmlessly against it
  at every level, even on a triple. Raising a barrier is the counter to Shock.

When one roll lands several different Nukes, they always fire **Starfall → Fire Magic → Shock**,
regardless of reel position. Shock going last is deliberate: a Fire Magic in the same roll gets the
chance to break the barrier before Shock checks for one.

Player-only reward cards can change how Starfall and Shock behave — see [§15](#15-post-battle-rewards).

---

## 8. Spells

Spells are the support action type. A side equips three.

- **Shield** — raises a barrier in front of the caster's Hero. It absorbs everything aimed at that side
  (highest targeting priority), grants no XP when hit or broken, and fully blocks Shock. Starfall
  ignores it. Rolling Shield again: **no barrier up** → summon one at the rolled level; **rolled level
  higher** → promote and fully refill it; **equal or lower** → heal it by a flat amount. Once destroyed,
  a new one can be raised the next turn.
- **Charm** (the "hack") — a chance to steal an enemy creature. It picks one living enemy creature
  whose class has a free stolen-slot on your board, ranks candidates by strength (current health plus
  100 per level), and targets the **weakest** at level 1, the **median** at level 2, the **strongest**
  at level 3. Success is rolled once:
  1. Base chance × number of living enemy creatures, capped at certain.
  2. Then a **creature-lead penalty**: −25 percentage points of that chance (multiplicatively) for each
     creature the caster *out-numbers* the target side by. Being behind gives no bonus; a 4-creature
     lead makes Charm impossible. This applies even when step 1 was already capped, so a side far ahead
     can't charm at a guaranteed 100%.

  A failed Charm consumes the roll and does nothing (unless improved — see [§15](#15-post-battle-rewards)).
  Charming an already-charmed creature steals it back. A stolen creature never returns to its home slot,
  keeps its own traits, and has any Battle Cry effect cleared whenever it changes sides.
- **Battle Cry** — buffs every creature on the caster's board and debuffs every enemy creature, both as
  damage multipliers lasting the rest of the battle phase. At level 3 the debuff is a total "energy
  drain": enemy creatures deal zero and skip their turns. A creature summoned later in the same turn
  still joins an active buff.

---

## 9. Heroes, Health & Win Condition

Each side has one Hero with a health pool. The player's Hero starts every fight at their run's Max HP
(base 100, plus any claimed Vitality cards). The enemy Hero's max health and avatar are defined per
fight.

Heroes take damage from:

1. Creature attacks — only once that side has no living creatures and no barrier.
2. Nukes — Fire Magic once everything ahead of the Hero is spent, and Starfall every time.
3. **Their own creatures dying** — 20% of the dead creature's max health.

Healing Shrooms can heal their own Hero.

**The battle ends the moment either Hero reaches zero health**, regardless of what creatures are still
standing (unless that Hero is a boss with another phase).

- **Victory** → after a short pause, the reward phase opens (if this fight has one), otherwise the run
  advances.
- **Defeat** → after the same pause, the locked Restart / Go to map menu appears.
- **A double knockout counts as a defeat.**

---

## 10. Reroll Energy

Energy is the player's currency for rerolling individual reels. It is a single run-long pool that
carries from fight to fight, shown on the battle HUD.

- A new run starts with **50** energy.
- **The first reroll of a roll phase costs 2**, and **each further reroll doubles the cost** — 2, 4,
  8, 16…
- The cost **resets to 2 whenever a roll phase ends** (a triple's bonus turn is a new roll phase).
- If the player can't afford the next reroll, the reroll buttons are disabled.

Energy is earned three ways, **none capped**:

1. **Triples** — every player triple (any action type) grants **+1** the moment the roll finishes,
   including a triple landed by a reroll.
2. **The fight's reward phase** — a guaranteed per-fight amount on victory (replays included).
3. **The Energy Cell reward card** (+20).

**A failed or abandoned attempt costs nothing — and keeps nothing.** Losing, restarting, or leaving via
Go to map reverts energy to exactly what the player had when that fight began. Winning carries both
spending and triple income forward.

The enemy doesn't use energy — it rerolls from its own per-fight budget, also shown on the HUD (see
[§11](#11-the-enemy-ai)).

---

## 11. The Enemy AI

The enemy plays the same game by the same rules, with a deliberately imperfect decision-maker. It
rolls from its own fight-specific creature roster, and the standard Nukes (Fire Magic, Starfall, Shock)
and Spells (Battle Cry, Charm, Shield). Player reward boosts never apply to it.

### Stupidity and critical failure

Two per-fight percentages make the AI miss its best move:

- **Stupidity** — the chance the AI does *not* take its top-ranked option. When it triggers, the AI
  drops one step down its ranked list and rolls again, so a bad streak can degrade several steps.
- **Critical failure** — rolled only after stupidity triggers. If it also triggers, the AI picks a
  completely random option.

Yes/no choices (summon or not, reroll or not) are a two-item list, so stupidity simply flips them.
Two certainties bypass stupidity: "always summon on the first turn" and "board full, nothing to summon."

### What the AI decides each turn

1. **Summon creatures, or cast something?**
   - Round 1: always summon.
   - If any of its own creatures are **stunned**, repairing them takes priority over everything, even
     a full board. It targets the healthiest stunned creature. Desire: 3 stunned → always; 2 → 80%;
     exactly 1 → by that creature's level (level 4 → 80%, level 3 → 50%, levels 1–2 → never).
   - Otherwise: a full board means no summon. Else by creature count — 0 → always; 1 and behind the
     player on creature count → 60%; 2 and behind → 40%; otherwise 15%.
2. **Which creature type?** Ranked: the stunned creature it decided to repair → Tank (if it has no
   creatures) → Archer (if any player creature is under 30% health) → Mage (if the player Hero is under
   30% health) → anything else it doesn't own.
3. **Which Nuke or Spell?** All six are scored against the current board and ranked. Scores react to
   things like whether the player has a barrier (it never shocks into one), how many enemies are hurt,
   how low the player Hero is. Charm's appetite scales with the same creature-lead penalty its real odds
   use.
4. **After a triple, it never chases the same action again** on the bonus roll (a *different* action
   of the same type is fine).

### Rerolls

The AI's rerolls come from a **per-fight budget** that depletes as it rerolls and refills only when the
fight restarts (or a boss changes phase). It never gains rerolls from triples.

Its per-turn allowance is a formula: it **reserves one reroll per 10 points of "effective health"** and
may spend the rest (always at least 1, never more than what's left). Effective health is its Hero's
current HP **minus the player's firepower lead** (if the player is ahead) — so it conserves when
healthy and winning, and opens up when hurt *or* out-gunned. It can earn **one extra reroll per turn**
the moment it completes a pair of the action it wanted.

Within a turn, it follows a one-way plan:

- **Open** — if any pair exists, chase it by rerolling the odd reel; once that reroll fires, it's
  committed to that pair. Otherwise, if it has more than one reroll, fish for its desired action
  instead, committing to that.
- **Chasing a pair** — keep rerolling the odd reel until the triple lands or the budget runs out. It
  never goes back to fishing, even if the odd reel shows its desired action — a triple is worth more.
- **Chasing a desired action** — if the desired action has a pair, chase it (and collect the bonus
  reroll). Otherwise keep fishing, ignoring incidental pairs.

**Below a per-fight desperation threshold of its own health, it stops rolling stupidity for rerolls
entirely** — it "rolls for its life" — and chases *any* pair rather than its preferred action.

---

## 12. Campaign Structure & the Map

A run walks a fixed list of 5 fights. Each fight defines:

- The enemy Hero's avatar, map icon, and maximum health.
- The enemy's creature roster — its own themed Tank, Archer, and Mage (demons, skeletons, orks, ghosts,
  the boss's undead). **Player and enemy roll from entirely separate pools.**
- Whether a loadout phase precedes it, whether a reward phase follows it, and the reward's guaranteed
  energy.
- The AI's stupidity, critical failure, reroll budget and desperation threshold.
- All the triple-rigging tuning from [§4](#4-the-slot-machine).

Each roster's traits make it play differently even where raw stats are close — the orks break
shields, the ghosts crit, the demons are fragile.

### The map

Between fights the player sees a map: one point per fight along a path. Points show future, current,
or complete, and reached points show their enemy's icon.

- **After a win that advances the run**, the map draws the path to the new point dot by dot, then the
  point becomes current and its enemy icon pops in.
- **Every reached point (current and beaten) pulses and is clickable.** Nothing starts on its own —
  except the very first fight, which has no loadout phase and starts automatically when first revealed
  or when resuming a fresh save.
- **Clicking a point** with a loadout phase plays an arrival pop, opens the loadout picker right on the
  map, and after it's confirmed plays a "here we go" shake and loads the battle. A point without a
  loadout phase goes straight to the shake and the battle.
- **Clicking a beaten point is a replay**: full fight, full reward pick, no progress change.
- Returning to the map via Go to map or after a replay never auto-starts anything.

Progress advances only on winning the newest fight *and* confirming any reward. Losing, restarting,
leaving, or replaying never advances progress or resets the wider run.

---

## 13. Multi-Phase Boss Fights

The Final Boss (the Skeleton Warlock) has a second phase. When his health hits zero:

1. His creatures die with him.
2. After a short pause he **rises**, with a screen shake and white flash, transformed into his burning
   form — a new avatar at full health (150), with a new, stronger crit-carrying roster and his own
   phase-2 AI and triple tuning. His reroll budget refills.
3. The player's creatures all die too, and their bodies are cleared.
4. The boss's new level-1 Archer, Tank and Mage are summoned onto the empty field.
5. Play resumes with the player's turn, which this time *is* followed by a battle.

Neither board wipe causes death-splash damage to either Hero. The round counter carries on; the
player's energy and Hero health carry over unchanged.

**Both phases must be won in one go.** The second phase has no map point of its own and is never saved
as progress: a defeat, restart, or quit during phase 2 returns to the start of phase 1.

---

## 14. Pre-Battle Loadout

Where enabled, the loadout picker opens on the map before the fight and lets the player rearrange
their **9 equipped slots**: one Tank, one Archer, one Mage, three Nukes, three Spells.

The screen shows:

- **Your deck** — the 9 currently equipped items.
- **Available** — everything gathered this run that isn't equipped.
- **A comparison panel** — the item in the chosen slot (left) versus the chosen replacement (right),
  each with name, type and description. Each side appears as soon as its half of the selection is made;
  clicking it clears that half.

Selection works **in either direction**: click an equipped slot or an Available item first, and the
other side greys out anything that couldn't match. Clicking a greyed item does nothing; clicking the
current selection again clears it. Nukes are interchangeable across the three Nuke slots, Spells across
the three Spell slots; creatures must match their class. Swap applies the pair; **nothing is committed
until Finish.**

A fresh run's own starting loadout is always available, so nothing can lock the player out of it.
Creatures unlocked from reward cards show up here.

---

## 15. Post-Battle Rewards

On victory, if the fight has a reward phase, it opens as an overlay on the battle screen:

1. **A guaranteed energy grant**, applied (and saved) immediately, amount set per fight.
2. **A choice of one of three drawn reward cards** — click a card to select it, then **Claim**.
   Selecting alone does nothing; the unchosen cards shrink away after claiming.

Replays grant the full reward again, including once-only cards still unclaimed.

### How the three cards are drawn

Once-only cards already owned are excluded. No card can appear twice in one draw. Then:

- **Slot 1** — a repeatable stat card: Vitality or Energy Cell.
- **Slot 2** — a creature unlock if any remain, otherwise the other stat card.
- **Slot 3** — a boost if any remain, otherwise a creature unlock, otherwise a stat card.

Once everything once-only is claimed, draws repeat stat cards.

### The card types

- **Vitality** — +15 Max HP for the rest of the run. Repeatable, stacking.
- **Energy Cell** — +20 reroll energy, once per claim. Repeatable.
- **Creature unlocks** (once each) — add Bulba, Lizard, or Healing Shroom to the loadout pool
  (see [§5](#5-creatures)).
- **Class boosts** (once each) — +20% damage *and* health for every player creature of one class
  (Archers, Tanks, or Mages), including unlocked variants.
- **Action boosts** (once each):
  - Fire Magic: +20% damage.
  - Shield: +20% barrier health and healing (summon, promote and heal).
  - Battle Cry: +60% of its *bonus* (×1.24 → ×1.38 at level 1); the enemy debuff is unchanged.
- **Improvements** (once each) — change how an action works:
  - **Improve Shock** — bolts hit a standing enemy barrier for 10 each instead of fizzling; once it
    breaks, the remaining bolts carry on and stun creatures normally.
  - **Improve Starfall** — after the volley, one extra star strikes a random enemy that survived it.
  - **Improve Charm** — a failed Charm has a 50% chance to stun its target instead.

**Every reward applies to the player only**, even where the enemy uses the same Nuke or Spell. A
creature the player charms from the enemy gets the player's class damage boost while on the player's
side, but keeps the health it was summoned with.

---

## 16. What Persists Across a Run

Carried from fight to fight, and saved:

- **The 9-slot loadout.**
- **Max HP** — base 100 plus every claimed Vitality card.
- **Reroll energy** — one uncapped run-long pool, including triple income from fights the player won.
- **Gathered items** — everything available in the loadout picker.
- **Claimed boosts and improvements.**
- **Furthest fight reached.**

Reset at the start of every fight (and not saved):

- Creatures on the field, their levels and experience.
- Barriers.
- Both Heroes' current health.
- The AI's reroll budget.
- All triple-rigging dial state.
- Energy spent or earned during an attempt that was lost, restarted or abandoned.
- Progress into a boss's second phase.

---

## 17. Balance Reference

### Starting loadout and stats

| | Value |
|---|---|
| Max HP | 100 |
| Starting reroll energy | 50 |
| Base reroll cost | 2 (doubles per reroll, resets each roll phase) |
| Energy per player triple | +1 |
| Tank | Golem |
| Archer | Bubka |
| Mage | Dragon |
| Nukes | Fire Magic, Starfall, Shock |
| Spells | Battle Cry, Charm, Shield |

### Creature roster

| Creature | Class | Traits | Where it appears |
|---|---|---|---|
| Bubka | Archer | — | Player starter |
| Golem | Tank | — | Player starter |
| Dragon | Mage | — | Player starter |
| Healing Shroom | Archer | Pre-battle heals (75% of damage per shot) | Reward unlock |
| Bulba | Tank | Never attacks, taunt, counterattacks | Reward unlock |
| Lizard | Mage | 35% crit for +100% | Reward unlock |
| Demon | Archer | — | Fights 1–2 |
| Cyclop | Tank | — | Fight 1 |
| Bat | Mage | — | Fights 1–2 |
| Skeleton | Tank | — | Fight 2 |
| Kodo | Archer | Shield-breaking ×1.5 | Fight 3 |
| Ork Tank | Tank | Shield-breaking ×1.5 | Fight 3 |
| Ork Mage | Mage | Shield-breaking ×1.5 | Fight 3 |
| Ghost Archer | Archer | 20% crit for +50% | Fight 4 |
| Ghost Tank | Tank | 20% crit for +50% | Fight 4 |
| Ghost Mage | Mage | 20% crit for +50% | Fight 4 |
| Skull Mummy | Archer | — (stats = Bubka) | Fight 5, phase 1 |
| Orc Warlord | Tank | — (stats = Golem) | Fight 5, phase 1 |
| Cursed Grimoire | Mage | — (stats = Dragon) | Fight 5, phase 1 |
| Blight Bloom | Archer | 20% crit for +50% | Fight 5, phase 2 |
| Obsidian Warden | Tank | 20% crit for +50% | Fight 5, phase 2 |
| Shadow Wyrm | Mage | 20% crit for +50% | Fight 5, phase 2 |

### Creature stats by level

Each cell is **damage / health / attacks per turn**.

**Archers**

| Creature | Level 1 | Level 2 | Level 3 | Level 4 |
|---|---|---|---|---|
| Bubka, Skull Mummy | 3 / 28 / 2 | 3 / 34 / 3 | 5 / 42 / 3 | 6 / 50 / 4 |
| Healing Shroom | 2 / 21 / 2 | 2 / 26 / 3 | 3.25 / 32 / 3 | 3.9 / 38 / 4 |
| Demon | 2 / 15 / 2 | 2 / 21 / 3 | 4 / 26 / 3 | 5 / 30 / 4 |
| Kodo | 3 / 34 / 2 | 3 / 39 / 3 | 5 / 50 / 3 | 6 / 60 / 4 |
| Ghost Archer | 4 / 28 / 2 | 3 / 34 / 4 | 4 / 42 / 4 | 6 / 50 / 5 |
| Blight Bloom | 4 / 32 / 2 | 4 / 39 / 3 | 6 / 48 / 3 | 7 / 58 / 4 |

**Tanks** (one attack per turn)

| Creature | Level 1 | Level 2 | Level 3 | Level 4 |
|---|---|---|---|---|
| Golem, Orc Warlord | 4 / 36 | 6 / 46 | 8 / 56 | 10 / 66 |
| Bulba | — / 36 | — / 46 | — / 56 | — / 66 |
| Cyclop | 3 / 24 | 4 / 31 | 6 / 39 | 8 / 42 |
| Skeleton, Ork Tank | 4 / 45 | 6 / 55 | 8 / 70 | 10 / 80 |
| Ghost Tank | 6 / 36 | 8 / 46 | 11 / 56 | 14 / 66 |
| Obsidian Warden | 5 / 41 | 7 / 53 | 10 / 64 | 12 / 76 |

Bulba's counterattack: 50% / 60% / 70% / 80% of each hit it takes, by level.

**Mages** (one attack per turn)

| Creature | Level 1 | Level 2 | Level 3 | Level 4 |
|---|---|---|---|---|
| Dragon, Cursed Grimoire | 9 / 24 | 15 / 28 | 22 / 32 | 34 / 42 |
| Lizard | 11.5 / 17 | 19 / 20 | 29 / 22 | 45 / 29 |
| Bat | 7 / 15 | 11 / 16 | 16 / 21 | 26 / 23 |
| Ork Mage | 9 / 28 | 15 / 35 | 22 / 40 | 34 / 52 |
| Ghost Mage | 11 / 24 | 17 / 28 | 26 / 32 | 40 / 42 |
| Shadow Wyrm | 11 / 28 | 18 / 32 | 26 / 37 | 40 / 48 |

A 20% / +50% crit averages ×1.1 damage; Lizard's 35% / +100% averages ×1.35.

**Experience, identical for every creature:**

| Level | Cumulative XP to reach | XP granted when killed at this level | Heal when re-rolled at this level |
|---|---|---|---|
| 1 | 0 | 30 | 3 |
| 2 | 120 | 60 | 7 |
| 3 | 320 | 120 | 10 |
| 4 | 600 | 240 | 24 |

Hitting a Hero grants 10× the damage dealt as XP, per hit.

### Nukes by match level

| Nuke | 1 reel | 2 reels | 3 reels | Notes |
|---|---|---|---|---|
| Fire Magic | 13 | 20 | 60 | Damage pool spread down the priority list |
| Starfall | 5 | 11 | 17 | Per target, hits every enemy creature and the Hero; ignores the barrier |
| Shock | 1 bolt | 2 bolts | 3 bolts | Each bolt stuns one creature; fully blocked by a barrier |

### Spells by match level

| Spell | 1 reel | 2 reels | 3 reels |
|---|---|---|---|
| Battle Cry — ally damage | ×1.24 | ×1.4 | ×1.8 |
| Battle Cry — enemy damage | ×0.4 | ×0.1 | ×0 (total drain) |
| Charm — base success chance | 12% | 21% | 35% |
| Shield — health when summoned | 15 | 27 | 56 |
| Shield — heal when re-rolled | 7 | 13 | 27 |

Charm's chance is × living enemy creatures (capped at 100%), then × (1 − 0.25 × the caster's creature
lead), floored at 0.

### Fight-by-fight progression

| # | Fight | Enemy Max HP | Enemy roster (Archer / Tank / Mage) | Roster trait | Loadout before? | Reward energy | AI stupidity | AI crit-fail | AI rerolls | AI desperation |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Tutorial | 45 | Demon / Cyclop / Bat | Fragile | No | +10 | 80% | 30% | 6 | 15% |
| 2 | Skeleton | 80 | Demon / Skeleton / Bat | Tough tank | Yes | +15 | 35% | 15% | 7 | 39.7% |
| 3 | Orks | 130 | Kodo / Ork Tank / Ork Mage | Shield-breaking | Yes | +30 | 25% | 10% | 10 | 39.7% |
| 4 | Ghosts | 150 | Ghost Archer / Ghost Tank / Ghost Mage | 20% crit for +50% | Yes | +40 | 20% | 10% | 13 | 55% |
| 5 | Final Boss, phase 1 | 150 | Skull Mummy / Orc Warlord / Cursed Grimoire | — | Yes | none (final) | 50% | 15% | 13 | 16% |
| 5 | Final Boss, phase 2 | 150 | Blight Bloom / Obsidian Warden / Shadow Wyrm | 20% crit for +50% | — | none (final) | 0% | 0% | 13 | 14.3% |

Lower stupidity/critical failure and a bigger reroll budget mean a sharper enemy. The Tutorial enemy is
very clumsy; the boss's first phase is deliberately sloppier than Fight 4, and his second phase plays
perfectly.

### Triple-rigging tuning per fight

| # | Fight | Neutral dirty dial | Round-1 dirty | Round-1 clean | Player clean base | Enemy clean base | Enemy clean stabilization | Reroll pity |
|---|---|---|---|---|---|---|---|---|
| 1 | Tutorial | 100 | 100 | 1 | 100 | 50 | −50 per bonus turn | +50 per reroll, rounds 1–3 |
| 2 | Skeleton | 100 | 60 | 50 | 100 | 100 | −25 per bonus turn | — |
| 3 | Orks | 100 | 110 | 50 | 100 | 100 | −25 per bonus turn | — |
| 4 | Ghosts | 100 | 110 | 50 | 100 | 100 | −50 per bonus turn | — |
| 5 | Final Boss (both phases) | 100 | 110 | 50 | 100 | 100 | −50 per bonus turn | — |

Dirty-dial streak stabilization and player clean-dial stabilization are 0 (off) in every fight.
Comeback decay divides positive bonuses by 3 per bonus turn in every fight. A round-1 clean dial of 1
means a fresh-spin triple is practically impossible on the Tutorial's opening turns.

### Comeback ladders per fight

Read each column as "at or below this health **or** out-gunned by at least this much → this adjustment,
biggest match wins."

| Fight | Side | ≤100% HP | ≤60% HP | ≤25% HP or adv ≥30 | ≤15% HP or adv ≥40 | ≤10% HP or adv ≥50 |
|---|---|---|---|---|---|---|
| 1 Tutorial | Player | −20 | 0 | +30 | +70 | +80 |
| 1 Tutorial | Enemy | −40 | −20 | 0 | +30 | +30 |
| 2 Skeleton | Player | −20 | 0 | +30 | +50 | +65 |
| 2 Skeleton | Enemy | −20 | 0 | +30 | +60 | +70 |
| 3–5 (all) | Player | −20 | 0 | +25 | +37 | +50 |
| 3–5 (all) | Enemy | −20 | 0 | +30 | +60 | +70 |

### Reward cards

| Card | Type | Effect | Repeatable? |
|---|---|---|---|
| Vitality | Status | +15 Max HP for the rest of the run, stacking | Yes |
| Energy Cell | Status | +20 reroll energy | Yes |
| Bulba | Creature | Unlocks Bulba (counter-tank) | Once |
| Lizard | Creature | Unlocks Lizard (crit mage) | Once |
| Healing Shroom | Creature | Unlocks Healing Shroom (healer archer) | Once |
| Boost Archers | Boost | +20% damage and health, all player archers | Once |
| Boost Tanks | Boost | +20% damage and health, all player tanks | Once |
| Boost Mages | Boost | +20% damage and health, all player mages | Once |
| Boost Fire Magic | Boost | +20% damage (≈16 / 24 / 72) | Once |
| Boost Shield | Boost | +20% health and healing (≈18 / 32 / 67 health) | Once |
| Boost Battle Cry | Boost | +60% of the ally bonus (×1.38 / ×1.64 / ×2.28) | Once |
| Improve Shock | Boost | Bolts deal 10 to a barrier, leftovers pass through | Once |
| Improve Starfall | Boost | +1 extra strike at a random survivor | Once |
| Improve Charm | Boost | Failed Charm: 50% chance to stun the target | Once |

---

## 18. Current Scope & Known Gaps

Things that exist in the design but aren't fully live, or are intentionally placeholder:

- **The campaign is a straight line** — the map visualizes a fixed order; replays are the only choice.
- **Completing the run has no ending screen yet** — the game simply stops.
- **No Nukes or Spells can be unlocked.** Reward cards only unlock creatures, so the loadout picker's
  Nuke/Spell slots have nothing to swap in today.
- **Normal hits have no generic hit feedback** — only crits (screen shake) and the final boss (hurt
  animation) react.
- **Energy has no cap** — the run's "capacity" value is unenforced.
- **Shield-breaking creatures are under-counted by the firepower estimate**, so they earn their opponent
  slightly less comeback help (and push the AI's reroll spending slightly less) than their real threat.
- **Some AI numbers are placeholders**: the 15% default summon chance, and "never repair a lone stunned
  level-1/2 creature."
- **Leaving via the pause menu while the reward overlay is open** abandons the fight: progress doesn't
  advance, though the guaranteed energy grant may already have been saved.
- **Only one difficulty path** — enemy toughness is fixed per fight.
- **A full battle can't yet run without its animations.** Individual pieces (combat, Nukes, Spells,
  rewards, loadout, XP, boss phase change) each have an instant path, but nothing composes them into a
  headless loop, so large-scale automated balance testing isn't possible yet.

---

*Compiled from `docs/GameLoop.md`, `docs/SlotMachine.md`, `docs/Battle.md`,
`docs/ActionsAndSpells.md`, `docs/Experience.md`, `docs/Energy.md`, `docs/AI.md`, `docs/Campaign.md`,
`docs/Encounters.md`, `docs/Loadout.md`, `docs/Rewards.md`, `docs/G.md`, `docs/AnimationAPI.md`, and
live balance data read from the game's creature/Nuke/Spell/fight/reward data assets.*
