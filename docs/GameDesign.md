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
touching any run progress: no loadout re-pick, no reward, no rewind of fights already won. Any reroll
energy spent during the failed attempt is refunded, so retrying costs nothing but time.

Completing the last fight in the sequence ends the run. (Today this is a simple stopping point — see
[§17](#17-current-scope--known-gaps).)

A run's state — loadout, Max HP, reroll energy, unlocked items, claimed rewards, current fight —
autosaves at well-defined points (a new run starting, entering each new fight, claiming a reward) so
a session can be closed and resumed without losing progress.

### The map between fights

Between fights the player sees a map: one point per fight in the run, connected along a path. Points
show one of three states — future, current, complete. On arriving at a new point, the map reveals the
path leading into it, pauses, then the point becomes current:

- If the fight has **no** loadout phase, the point plays a "you've arrived, here we go" shake and the
  battle loads.
- If it **does**, the point plays a softer arrival pop instead, the loadout picker opens right there
  on the map, and only after the player confirms it — following another short pause — does the fight
  shake play and the battle load.

Completing a loadout pick does not by itself advance run progress; only winning the fight (and
confirming any reward that follows) moves the player to the next point.

---

## 3. The Battle: Round & Turn Flow

A battle is a sequence of **rounds**. One round is the player's turn followed by the enemy's turn.

Each side's turn, in order:

1. **Roll** — the active side spins the slot machine and settles on three results.
2. **Resolve** — depending on which of the three action types was rolled, the turn either summons/
   upgrades creatures, fires Nukes, or casts Spells.
3. **Battle** — every creature on both sides attacks automatically (see [§6](#6-combat-resolution)).
4. **Cleanup** — dead bodies are removed, Battle Cry effects expire, and XP gems fly to the creatures
   that earned them.

**Round 1 is special: the player's turn is not followed by a battle phase.** The player summons their
opening creatures and is not immediately attacked for it. The enemy's turn always ends in a battle
phase, including in round 1.

**Landing a triple grants a bonus turn.** If all three reels land the same result, the acting side
immediately takes another full turn — roll, resolve — before play passes on. This can chain: a bonus
turn that also lands a triple grants another. A triple's bonus turn does *not* advance the round
counter; the round only ticks over once both sides have finished acting.

The battle ends the instant either Hero's health reaches zero — see [§9](#9-heroes-health--win-condition).

A battle can be paused at any time, which genuinely freezes everything (animations, timers, pending
effects), and restarted in place from the pause menu.

---

## 4. The Slot Machine

The heart of the game. Three reels, and — before spinning — a choice of which of three **action
types** to roll:

- **Creature** — the three reels show the side's Tank, Archer, and Mage.
- **Nuke** — the three reels show the side's three equipped Nukes.
- **Spell** — the three reels show the side's three equipped Spells.

The action type can only be switched before the first spin of a turn. Once the reels are spinning,
the choice is locked for that turn.

### How a roll resolves into strength

After the reels land, identical results are grouped. Each distinct result produces one action, and
**how many reels showed it becomes that action's strength level** (1, 2, or 3):

- **One reel** → that action fires at level 1.
- **Two reels** → level 2, noticeably stronger.
- **Three reels (a triple)** → level 3, the strongest version — *and* the acting side gets a bonus
  turn.

A mixed roll fires several actions at once. Rolling Fire Magic, Fire Magic, Starfall means a level-2
Fire Magic *and* a level-1 Starfall both resolve this turn.

For Creature rolls, the level is the level the creature is summoned or promoted **to** — rolling two
Tanks puts a level-2 Tank on the board directly.

### Rerolling

Once all three reels land — unless it's already a triple — the player may reroll individual reels,
paying energy each time (see [§10](#10-reroll-energy)), then hit Finish to lock the result in. A
triple skips this entirely: it auto-finishes, with no reroll opportunity.

### Triple odds are tuned, not flat

The odds of getting a triple are deliberately rigged, per side, and shift over the course of a fight.
Two separate dials govern it, both on a **0–200 scale where 100 is neutral**: 0 means "never," 100
means genuinely unbiased (a real 1-in-3), 200 means "always." The scale is not linear across its whole
range — it passes through three fixed points: 0 → 0%, 100 → 33.3%, 200 → 100%.

- **The clean-triple dial** is checked once per fresh spin, before the reels are decided at all. If it
  succeeds, the roll short-circuits straight into an all-three-matching triple. If it fails, the first
  two decided reels are forced to differ, so a fresh spin can never produce a triple *by accident* —
  every triple on a fresh spin comes from this deliberate check.
- **The dirty-triple dial** governs the other route: when two reels already match and a third is being
  decided (which in practice means a reroll), this dial sets the odds that third one completes the
  triple.

Which physical reel ends up being "the one that completes it" is randomized every roll, so no single
reel is ever visibly the decider.

Both dials are re-derived at the start of every fresh turn, and both are per-side and per-fight tuned.

### Comeback assistance

Both dials get the same **comeback adjustment** added to them at the start of each side's turn — the
mechanic that quietly helps whoever is losing. Each fight defines a ladder of comeback rules per side,
and each rule has three parts:

1. **An HP threshold** — "this side's Hero is at or below X% health."
2. **A firepower-advantage threshold** — "the opponent out-guns this side by at least N points."
3. **A triple adjustment** — the number added to both dials when the rule applies.

A rule fires if **either** of its two conditions is met — low health *or* being out-gunned is enough
on its own. A rule with its advantage threshold left at zero is a pure health rule. Of every rule that
fires, **the largest adjustment wins** (they never stack). Adjustments can be negative, which is how
the game *punishes* a side that's comfortably ahead: the top rung of every fight's ladder is a flat
−20 that applies at any health, and it only stops being the winning rule once a lower rung actually
fires.

The net effect of the shipped ladders (identical in shape across all fights, see
[§16](#16-balance-reference) for the per-fight numbers):

| This side's health | No firepower disadvantage | Out-gunned by 30+ | by 40+ | by 50+ |
|---|---|---|---|---|
| 100% – 61% | −20 (harder triples) | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 60% – 26% | 0 (neutral) | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 25% – 16% | tier-1 bonus | tier-1 bonus | tier-2 bonus | tier-3 bonus |
| 15% – 11% | tier-2 bonus | tier-2 bonus | tier-2 bonus | tier-3 bonus |
| 10% – 0% | tier-3 bonus | tier-3 bonus | tier-3 bonus | tier-3 bonus |

**"Firepower" means total damage output per battle phase** — for each side, the sum over all its
creatures of damage-per-hit × hits-per-turn, with stunned creatures counting as zero. **The opposing
barrier is then subtracted**: a Shield standing in a side's way absorbs damage before anything else,
so its health is deducted from that side's effective firepower, floored at zero. A side facing a
barrier bigger than its whole offense reads as zero firepower, not negative. "Advantage" is simply
the difference between the two sides' effective firepower.

One consequence worth knowing: shield-breaking creatures (which deal 1.5× to barriers) are *not*
counted at their bonus rate in this estimate, because no target has been picked when it's computed —
so a shield-breaking roster looks slightly weaker than it really is, and earns its opponent slightly
less comeback help than it strictly should.

**A comeback bonus decays as it works.** Every time a side lands a triple and takes another bonus
turn, a *positive* comeback adjustment is halved, and both dials drop by the same amount — 70 → 35 →
17 → 8 and so on. So a big comeback boost helps land the turn's *first* triple without fuelling an
endless streak off it. A *negative* adjustment (the high-health punish) is never halved and stays for
the whole turn — otherwise landing a triple would soften your own penalty. The whole thing is
recomputed from scratch on the side's next fresh turn.

**Round 1 ignores comeback assistance entirely** — both dials are forced to fixed per-fight opening
values for both sides' first turns.

### Two further tuning levers

- **Streak stabilization** — each time a side chains another bonus turn off a triple, its dials can be
  pushed back down by a per-fight amount, so a hot streak can't run forever. This applies on top of
  (and after) comeback halving, and resets on the side's next fresh turn.
- **Reroll pity ("ludo progress")** — during a side's *first* roll phase of a turn only, every reroll
  nudges the dirty-triple dial upward by a per-fight amount, making a triple progressively likelier
  the more the player fishes for one. It's undone completely the moment that roll phase ends, and is
  switched off for the rest of a turn once that side has already landed one triple. The amount can be
  tuned per round number.

---

## 5. Creatures

Each side fields up to **three creatures of its own** — one Tank, one Archer, one Mage — in three
dedicated slots. There are additionally two "stolen" slots per class, which only ever fill via the
Charm spell (see [§8](#8-spells)).

The three archetypes:

- **Tank** — highest health, lowest damage, single attack, and the only melee attacker: it physically
  runs at its target, strikes, and leaps back.
- **Archer** — moderate health and low damage per hit, but **multiple attacks per turn**, re-picking
  its target after each hit. This lets an Archer finish off a weakened target and immediately move on
  to the next.
- **Mage** — lowest health, highest single-hit damage, one attack per turn.

### Summoning and promotion

Rolling a creature type either summons it (if that slot is empty) or, if one is already there,
**promotes it to the rolled level** if that's higher — or **heals it** by a fixed amount if it isn't.
Promotion fully refills health at the new level's maximum; it doesn't just raise the ceiling.

Both promotion and healing **clear a creature's stun**, which makes re-rolling your own creature type
a legitimate way to un-stun it.

### Levels and experience

Creatures level from 1 to 4, either by being rolled at a higher level directly or by earning enough
experience in combat. Damage, health, and attack count all scale with level.

Experience is earned during battle and paid out afterward as gems that visibly fly from the kill
location to the creature that earned it:

- **Killing a creature** grants experience based on the *victim's* level, awarded once per attacker
  that landed on that kill.
- **Hitting a Hero** grants experience worth 10× the damage dealt, on every hit, kill or not.
- **Hitting a barrier grants nothing.**

If a creature dies before its gem lands, that experience is simply lost — it isn't redistributed.

### Death splashes onto your own Hero

**Whenever a creature dies, its own side's Hero takes damage equal to 20% of that creature's maximum
health, rounded down.** This applies to both sides, and to any cause of death. It's based on maximum
health, so it costs the same whether the creature died on its last point of health or was massively
overkilled — losing a big creature genuinely hurts.

---

## 6. Combat Resolution

After a side's turn resolves, every creature on the field attacks, both sides at once. The whole
exchange is planned up front against a snapshot of everyone's health, then played out — so the
outcome is fixed the moment the battle starts, and the animation is just presentation.

### Targeting priority

Every attacker picks its target by the same rule, re-evaluated for each individual hit:

1. **The opposing barrier**, if one is up and alive — always first.
2. Otherwise the highest-priority living opposing **creature**, in the fixed order **Mage → Archer →
   Tank**.
3. Otherwise the opposing **Hero**.

A Hero is never attacked while any of its creatures still live, and a barrier always takes priority
over both. That same Mage → Archer → Tank order also decides which attacker acts first.

### Per-attacker rules

- A **stunned** creature skips its turn entirely.
- Damage is the creature's level damage, multiplied by any active Battle Cry buff or debuff. If a
  debuff reduces damage to zero or less, the attacker skips its turn.
- An attacker fires its full attack count, re-picking a target for each hit.
- Some creatures carry a **shield-breaking** trait: they deal **1.5× damage against barriers only**.
  Today the whole ork roster (Kodo, Ork Tank, Ork Mage) has it. These multiply on top of the Battle Cry
  multiplier — a level-1 Kodo (3 damage) under a doubled Battle Cry hits a barrier for 9.

### Choreography

Ranged attackers all fire simultaneously from their slots. Tanks are handled specially, because they
physically travel to their target:

- **Two tanks targeting each other** duel: one runs in and attacks, and the other launches its own
  attack as the first arrives.
- **Multiple tanks sharing one target** take turns in a queue, each waiting for the previous one to
  leap back, so they don't pile onto the same spot at once.

A target killed mid-attack defers its death animation until the attack finishes, so a tank isn't left
swinging at a corpse.

---

## 7. Nukes

Nukes are the offensive action type. A side equips three. Rolling them fires them at the levels the
roll produced.

- **Fire Magic** — a single damage pool spread down a priority list: the barrier first (unless the
  Nuke ignores barriers), then all living Mages, then Tanks, then Archers, then the Hero. Each target
  absorbs up to its remaining health, and the pool moves on until it's spent.
- **Shock** — stuns instead of damaging. A stunned creature skips its combat turn until it's healed,
  promoted, or killed. (Its damage numbers exist in the game's data but aren't used — see
  [§17](#17-current-scope--known-gaps).)
- **Starfall** — **ignores the barrier entirely** and strikes past it. Much lower raw damage than the
  others, but it can't be blocked.
- **Fireball** — a fourth Nuke in the game's data with no way to obtain it in a run today.

---

## 8. Spells

Spells are the support action type. A side equips three.

- **Shield** — raises a barrier in front of the caster's Hero. It's a separate object that absorbs
  everything aimed at that side (highest targeting priority) and grants no experience when hit or
  killed. Rolling Shield again behaves three ways: **no barrier up** → summon one at the rolled level;
  **rolled level higher than the current one** → promote and fully refill it; **rolled level equal or
  lower** → heal it by a flat amount. Once destroyed, the slot frees immediately so a new barrier can
  be raised the next turn.
- **Charm** (the "hack" mechanic) — a chance to steal an enemy creature and turn it to fight for you.
  It picks one living enemy creature whose class has a free stolen-slot on your board, ranks the
  candidates by a strength score (current health plus 100 per level), and targets the **weakest** at
  level 1, the **median** at level 2, and the **strongest** at level 3. Success is rolled once, at a
  chance scaled by how many enemy creatures are alive (base chance × living count, capped at
  certainty). A failed Charm consumes the roll and does nothing. Charming an already-charmed creature
  steals it back. A creature only ever changes sides through Charm, and once it leaves its home slot
  it never returns to one. Any Battle Cry effect on it is cleared whenever it changes sides, since
  that effect was computed for the side it just left.
- **Battle Cry** — buffs every creature on the caster's board and debuffs every enemy creature, both
  as flat damage multipliers lasting the rest of the battle phase. At level 3 the debuff is a total
  "energy drain": enemy creatures deal zero and skip their turns entirely. A creature summoned later
  in the same turn still joins an active buff.

---

## 9. Heroes, Health & Win Condition

Each side has one Hero with a health pool. The player's Hero starts every fight at their run's Max HP
(base 100, plus any claimed Vitality rewards). The enemy Hero's max health is defined per fight.

Heroes take damage from three sources:

1. Attacks that get through — only once that side has no living creatures and no barrier.
2. Nukes aimed past the front line (notably Starfall, which ignores barriers outright).
3. **Their own creatures dying** — 20% of the dead creature's maximum health, every time (see
   [§5](#5-creatures)).

**The battle ends the moment either Hero reaches zero health**, regardless of what creatures are still
standing. Each fight's enemy Hero is effectively the boss: kill it and the fight is won immediately.

- **Victory** → after a short pause, either the reward phase opens (if this fight has one) or the run
  advances straight to the next fight.
- **Defeat** → after the same pause, the current fight restarts from the beginning.
- **A double knockout is treated as a defeat.**

---

## 10. Reroll Energy

Energy is the player's currency for rerolling individual reels. It is **not** per-battle — it's a
single run-long pool that carries from fight to fight.

- A new run starts with **50** energy.
- **The first reroll of a roll phase costs 2**, and **each subsequent reroll doubles the cost** — 2,
  4, 8, 16, and so on.
- The cost **resets to 2 whenever a roll phase ends**, so the escalation is per roll phase, not per
  battle or per run.
- If the player can't afford the next reroll, the reroll button is simply unavailable.

Energy is replenished by winning fights: each fight with a reward phase grants a guaranteed amount,
and the Energy Cell reward card grants more. **Neither grant is capped** — the pool can climb
indefinitely.

**A failed attempt costs nothing.** Losing (or manually restarting) a fight reverts energy to exactly
what the player had when that fight began, so retrying isn't punished. Winning carries the spend
forward as normal.

The enemy does not use energy — it rerolls from its own separate budget (see [§11](#11-the-enemy-ai)).

---

## 11. The Enemy AI

The enemy plays the same game by the same rules, with a deliberately imperfect decision-maker.

### Stupidity and critical failure

Two per-fight percentages make the AI miss its own best move:

- **Stupidity** — the chance the AI does *not* take its top-ranked option. When it triggers, the AI
  drops one step down its ranked list and rolls again, so a bad streak can degrade it several steps.
- **Critical failure** — rolled only *after* stupidity triggers. If it also triggers, the AI abandons
  ranking entirely and picks a completely random option.

Binary choices (summon or not, reroll or not) model this as a two-item list, so a stupidity trigger
simply flips the decision.

A few decisions deliberately bypass stupidity, because they're certainties rather than preferences:
the "always summon on the first turn" rule, and the "board is full, nothing to summon" rule.

### What the AI decides each turn

1. **Summon creatures, or cast something?**
   - Turn 1: always summon.
   - If any of its own creatures are **stunned**, repairing them takes priority over everything —
     including a full board, since repairing needs no free slot. It targets the healthiest stunned
     creature. Desire scales with how many are stunned: 3 stunned → always; 2 → 80%; exactly 1 →
     depends on that creature's level (level 4 → 80%, level 3 → 50%, levels 1–2 → never).
   - Otherwise: a full board means no summon. Else desire is keyed to creature count — 0 creatures →
     always; 1 creature and behind the player → 60%; 2 and behind → 40%; anything else → 15%.
2. **Which creature type?** Ranked: a stunned creature it decided to repair → Tank (if it has none at
   all) → Archer (if any player creature is under 30% health) → Mage (if the player Hero is under 30%
   health) → anything else it doesn't already own.
3. **Which Nuke or Spell?** All six of its equipped actions are scored against the current board and
   ranked. Each action scores itself on things like whether the player has a barrier, how many enemies
   are hurt, and how low the player Hero is.
4. **After a triple, it never chases the same action twice.** The bonus roll excludes whatever just
   tripled.

### Rerolls

The AI's rerolls come from a **fight-wide budget**, not per-turn energy. It depletes as the AI rerolls
and only refills when the fight restarts.

Its per-turn allowance is a formula, not a fixed number: the AI reserves roughly one reroll per 10
points of its own current Hero health, and spends the rest. So it conserves at full health and opens
up as it gets hurt. It can also earn **one extra reroll per turn** the moment it sees it has completed
its desired action's pair while chasing it.

Within a turn, the AI runs a small one-way state machine:

- **Open** — if any pair exists among the three reels (desired or not), chase it by rerolling the odd
  reel; the instant that chase reroll fires, the turn locks into chasing that pair. Otherwise, if it
  has more than one reroll available, fish for its desired action instead — and lock into that.
- **Chasing a pair** — keep rerolling the odd reel until the triple lands or the budget runs out. It
  never goes back to fishing, and it won't spare the odd reel even if that reel happens to be showing
  its desired action: a triple is worth more.
- **Chasing a desired action** — if the desired action has a pair, chase it exactly like above (and
  collect the one-time bonus reroll). Otherwise keep fishing, deliberately ignoring any incidental
  pair it happens to form.

**Below a per-fight desperation threshold of its own Hero health, the AI stops rolling stupidity for
rerolls at all** — "it rolls for its life" — and also stops caring about its specific desired action,
chasing *any* pair it finds instead.

---

## 12. Campaign Structure

A run walks a fixed list of 5 fights in order. Each fight defines:

- The enemy Hero's avatar and maximum health.
- The enemy's creature roster — its own Tank, Archer, and Mage. **Player and enemy roll from entirely
  separate pools**: the player's comes from their run loadout, the enemy's from the fight's own
  roster. (A fight with no roster of its own falls back to mirroring the player's — which is what
  fights 4 and 5 currently do.)
- Whether a loadout phase precedes it, and whether a reward phase follows it (and how much guaranteed
  energy that reward grants).
- The AI's stupidity, critical-failure, reroll budget and desperation threshold.
- All of the triple-rigging tuning from [§4](#4-the-slot-machine): the neutral and opening dial values,
  both sides' comeback ladders, streak stabilization, and reroll pity.

Progress advances only on winning a fight *and* confirming any reward that follows. Losing, or
restarting from the pause menu, never advances progress, never re-grants a reward, and never resets
the wider run.

---

## 13. Pre-Battle Loadout

Where enabled, the loadout picker opens on the map before the fight and lets the player rearrange
their **9 equipped slots**: one Tank, one Archer, one Mage, three Nukes, three Spells.

The screen shows:

- **Your deck** — the 9 currently equipped items.
- **Available** — everything gathered this run that isn't currently equipped.
- **A comparison panel** — side by side, what's currently in the chosen slot versus what would replace
  it.

Selection is **bidirectional**: the player can click an equipped slot first or a pool item first, and
whichever is picked first greys out everything on the other side that couldn't match it. Clicking a
greyed item does nothing; clicking the current selection again clears it. Nukes and Spells are
interchangeable within their own three slots — any Nuke fits any Nuke slot — while creatures must
match their class.

Swapping updates the pending loadout and refreshes the pool; **nothing is committed until the player
presses Finish.** Browsing, comparing, and even swapping back and forth never advances the game on its
own.

A fresh run's own starting loadout is always available in its own picker, so nothing can lock the
player out of what they started with.

---

## 14. Post-Battle Rewards

On victory, if the fight has a reward phase, it opens as an overlay right on the battle's game-over
screen.

Two things happen:

1. **A guaranteed energy grant**, applied immediately on the screen opening, in an amount defined per
   fight.
2. **A choice of one of three drawn reward cards**, applied when the player presses Claim.

### How the three cards are drawn

Cards already owned (for the once-only kinds) are excluded from the draw. Then:

- **Slot 1** is always a repeatable stat card — Vitality or Energy Cell.
- **Slot 2** prefers a creature unlock, falls back to a boost, then to a stat card.
- **Slot 3** prefers a boost, falls back to a creature unlock, then to a stat card.

The same once-only card can't appear twice in one draw. As the player claims things, the draw
degrades gracefully — once all creature unlocks are claimed a draw becomes stat + boost + boost, and
once boosts are gone too it becomes three stat cards.

### The card types

- **Vitality** — +15 Max HP for the rest of the run. Repeatable and **stacking**.
- **Energy Cell** — a one-off grant of reroll energy. Repeatable.
- **Creature unlocks** — add a bigger variant (Bubka Big, Golem Big, Dragon Big) to the pool the
  loadout picker draws from. Once each.
- **Boosts** — a permanent **+20%** to one specific action's numbers for the rest of the run. Once
  each. What "+20%" means depends on the action: creature damage *and* health, Nuke damage, Charm's
  success chance, Battle Cry's buff (not its debuff), or the Shield's health (which covers summoning,
  promoting, and healing it).

**Boosts apply to whichever side rolls that action** — they're attached to the action, not to the
player — so a boosted creature or Nuke also hits harder if the enemy happens to field the same one.

Only pressing Claim finalizes anything; merely selecting a card does not.

---

## 15. What Persists Across a Run

Carried from fight to fight, and saved:

- **The 9-slot loadout** — Tank, Archer, Mage, 3 Nukes, 3 Spells.
- **Max HP** — base 100 plus every claimed Vitality card.
- **Reroll energy** — a single run-long pool, uncapped.
- **Gathered items** — everything unlocked and therefore available in the loadout picker.
- **Claimed boosts** — each permanently improving its action by 20%.
- **Current position in the run.**

Explicitly **not** persistent — reset at the start of every fight:

- Creatures on the field, and their levels and experience.
- Barriers.
- Both Heroes' current health.
- The AI's reroll budget.
- All triple-rigging dial state.

---

## 16. Balance Reference

### Starting loadout and stats

| | Value |
|---|---|
| Max HP | 100 |
| Starting reroll energy | 50 |
| Base reroll cost | 2 (doubles per reroll, resets each roll phase) |
| Tank | Golem |
| Archer | Bubka |
| Mage | Dragon |
| Nukes | Fire Magic, Starfall, Shock |
| Spells | Battle Cry, Charm, Shield |

### Creature stats by level (damage / health / attacks per turn)

Several creatures share a stat block. Grouped by what's actually distinct:

**Archers** — Bubka (player default), Demon, Bubka Big:

| Level | Damage | Health | Attacks | Damage/turn |
|---|---|---|---|---|
| 1 | 3 | 28 | 2 | 6 |
| 2 | 3 | 34 | 3 | 9 |
| 3 | 5 | 42 | 3 | 15 |
| 4 | 6 | 50 | 4 | 24 |

**Kodo** (ork archer — shield-breaking, tougher):

| Level | Damage | Health | Attacks | Damage/turn |
|---|---|---|---|---|
| 1 | 3 | 34 | 2 | 6 |
| 2 | 3 | 39 | 3 | 9 |
| 3 | 5 | 50 | 3 | 15 |
| 4 | 6 | 60 | 4 | 24 |

**Mages** — Dragon (player default), Bat, Dragon Big:

| Level | Damage | Health | Attacks |
|---|---|---|---|
| 1 | 9 | 24 | 1 |
| 2 | 15 | 28 | 1 |
| 3 | 22 | 32 | 1 |
| 4 | 34 | 42 | 1 |

**Ork Mage** (shield-breaking, tougher):

| Level | Damage | Health | Attacks |
|---|---|---|---|
| 1 | 9 | 28 | 1 |
| 2 | 15 | 35 | 1 |
| 3 | 22 | 40 | 1 |
| 4 | 34 | 52 | 1 |

**Tanks** — Golem (player default), Cyclop, Golem Big:

| Level | Damage | Health | Attacks |
|---|---|---|---|
| 1 | 4 | 36 | 1 |
| 2 | 6 | 46 | 1 |
| 3 | 8 | 56 | 1 |
| 4 | 10 | 66 | 1 |

**Skeleton / Ork Tank** (tougher; Ork Tank is shield-breaking):

| Level | Damage | Health | Attacks |
|---|---|---|---|
| 1 | 4 | 45 | 1 |
| 2 | 6 | 55 | 1 |
| 3 | 8 | 70 | 1 |
| 4 | 10 | 80 | 1 |

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
| Shock | 14 | 30 | 60 | Damage values unused — Shock only stuns |
| Starfall | 5 | 11 | 17 | Ignores the barrier entirely |
| Fireball | 14 | 30 | 60 | Not obtainable in a run today |

### Spells by match level

| Spell | 1 reel | 2 reels | 3 reels |
|---|---|---|---|
| Battle Cry — ally damage | ×1.24 | ×1.4 | ×1.8 |
| Battle Cry — enemy damage | ×0.4 | ×0.1 | ×0 (total drain) |
| Charm — base success chance | 12% | 21% | 35% |
| Shield — health when summoned | 15 | 27 | 56 |
| Shield — heal when re-rolled | 7 | 13 | 27 |

Charm's chance is multiplied by the number of living enemy creatures, capped at certain. Its target
pick is weakest / median / strongest at levels 1 / 2 / 3, ranked by current health plus 100 per level.

### Fight-by-fight progression

| # | Fight | Enemy Max HP | Enemy roster | Loadout before? | Reward after? | Reward energy | AI stupidity | AI crit-fail | AI rerolls | AI desperation |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Tutorial | 45 | Demon / Cyclop / Bat | No | Yes | +10 | 50% | 20% | 6 | 15% |
| 2 | Skeleton | 80 | Demon / Skeleton / Bat | Yes | Yes | +15 | 35% | 15% | 7 | 39.7% |
| 3 | Orks | 130 | Kodo / Ork Tank / Ork Mage | Yes | Yes | +30 | 25% | 10% | 10 | 39.7% |
| 4 | Elite | 1 *(placeholder)* | *none — mirrors the player* | Yes | Yes | +50 | 20% | 15% | 10 | 15% |
| 5 | Final Boss | 1 *(placeholder)* | *none — mirrors the player* | Yes | No | — | 20% | 15% | 10 | 15% |

Lower stupidity/critical-failure and a bigger reroll budget all mean a *sharper* enemy — the AI is at
its clumsiest and most reroll-starved on the Tutorial and sharpest from Fight 3 on.

### Triple-rigging tuning per fight

| # | Fight | Neutral dirty dial | Round-1 dirty | Round-1 clean | Player clean base | Enemy clean base | Enemy streak stabilization | Reroll pity |
|---|---|---|---|---|---|---|---|---|
| 1 | Tutorial | 100 | 100 | 50 | 100 | 50 | — | +50 per reroll, rounds 1–3 |
| 2 | Skeleton | 100 | 60 | 50 | 100 | 100 | −25 per bonus turn | — |
| 3 | Orks | 100 | 110 | 50 | 100 | 100 | −25 per bonus turn | — |
| 4 | Elite | 100 | 50 | 50 | 100 | 100 | — | — |
| 5 | Final Boss | 100 | 50 | 50 | 100 | 100 | — | — |

Dirty-triple streak stabilization is 0 (off) on every fight today, and the player's clean-triple
stabilization is 0 on every fight — only the enemy's is used, on fights 2 and 3.

### Comeback ladders per fight

Every ladder has the same 5 rungs and the same advantage thresholds; only the three bonus tiers vary.
Read each row as "at or below this health **or** out-gunned by at least this much → this adjustment,
biggest match wins."

| Fight | Side | ≤100% HP (no adv.) | ≤60% HP (no adv.) | ≤25% HP or adv ≥30 | ≤15% HP or adv ≥40 | ≤10% HP or adv ≥50 |
|---|---|---|---|---|---|---|
| 1 Tutorial | Player | −20 | 0 | +30 | +70 | +80 |
| 1 Tutorial | Enemy | −20 | 0 | +30 | +50 | +65 |
| 2 Skeleton | Player | −20 | 0 | +30 | +50 | +65 |
| 2 Skeleton | Enemy | −20 | 0 | +30 | +60 | +70 |
| 3 Orks | Player | −20 | 0 | +35 | +55 | +75 |
| 3 Orks | Enemy | −20 | 0 | +30 | +60 | +70 |
| 4 Elite | Both | −20 | 0 | +30 | +50 | +65 |
| 5 Final Boss | Both | −20 | 0 | +30 | +50 | +65 |

Adjustments are added to both triple dials (base 100 = neutral, 200 = guaranteed), and a positive one
halves on each bonus turn the side chains.

### Reward cards

| Card | Type | Effect | Repeatable? |
|---|---|---|---|
| Vitality | Status | +15 Max HP for the rest of the run, stacking | Yes |
| Energy Cell | Status | +20 reroll energy, once | Yes |
| Bubka Big | Creature | Unlocks Bubka Big for the loadout | Once |
| Golem Big | Creature | Unlocks Golem Big for the loadout | Once |
| Dragon Big | Creature | Unlocks Dragon Big for the loadout | Once |
| Boost Bubka | Boost | +20% Bubka damage and health | Once |
| Boost Golem | Boost | +20% Golem damage and health | Once |
| Boost Dragon | Boost | +20% Dragon damage and health | Once |
| Boost Fire Magic | Boost | +20% Fire Magic damage | Once |
| Boost Starfall | Boost | +20% Starfall damage | Once |
| Boost Shock | Boost | No effect yet — Shock has no boostable number | Once |
| Boost Battle Cry | Boost | +20% Battle Cry buff strength | Once |
| Boost Charm | Boost | +20% Charm success chance | Once |
| Boost Shield | Boost | +20% Shield health | Once |

---

## 17. Current Scope & Known Gaps

Things that exist in the game's data/design but aren't fully live yet, or are intentionally
placeholder — worth knowing before treating this document as describing a 100%-finished game:

- **Only 5 fights are authored**, and the last two (Elite, Final Boss) are unfinished: both have a
  placeholder enemy Hero of **1 max HP** and no enemy roster of their own, so they currently mirror
  whatever the player has equipped and end instantly.
- **The campaign is a straight line, not a branching path** — the map visualizes a fixed order rather
  than offering a choice of route.
- **Completing the run has no dedicated ending screen yet** — it's currently a simple, unmistakable
  stop rather than a victory celebration/summary.
- **Boost Shock is inert** — Shock has no numeric stat a Boost card could improve, so the card exists
  but does nothing.
- **Shock carries unused damage numbers** (14/30/60) in its data. It only stuns; those numbers are
  vestigial and are not dealt.
- **Fireball (a 4th Nuke)** exists in the data with no unlock or reward path, so it can't be obtained.
- **Energy Cell's name and description say "+50" but it grants +20.** The description is stale
  relative to the actual value.
- **The three "Big" creature unlocks are statistically identical to their base versions today** —
  Bubka Big, Dragon Big, and Golem Big have exactly the same damage/health/attack numbers as Bubka,
  Dragon, and Golem, despite their descriptions promising a stronger version. Claiming one currently
  changes nothing but the art.
- **Gathered creatures from reward cards can be equipped, but nothing else uses them** — they exist as
  loadout options only.
- **Boosts don't distinguish sides** — a boost improves its action for whoever rolls it, including the
  enemy if it fields the same creature.
- **A run offers only one difficulty path** — enemy toughness is fixed per fight.
- **Energy has no hard cap** — nothing limits how high the reroll-energy pool can climb; the
  "capacity" value in the run's data is unenforced.
- **Shield-breaking creatures are under-counted by the comeback system's firepower estimate**, so they
  earn their opponent slightly less comeback assistance than their real threat warrants.
- **A battle cannot yet be run without its animations.** Individual pieces (combat, Nukes, Spells,
  rewards, loadout, XP) each have an instant-resolve path, but nothing composes them into a headless
  loop, so automated large-scale balance testing isn't possible yet.

---

*Compiled from `docs/GameLoop.md`, `docs/SlotMachine.md`, `docs/Battle.md`,
`docs/ActionsAndSpells.md`, `docs/Experience.md`, `docs/Energy.md`, `docs/AI.md`, `docs/Campaign.md`,
`docs/Encounters.md`, `docs/Loadout.md`, `docs/Rewards.md`, `docs/G.md`, and live balance data read
from the game's creature/Nuke/Spell/fight/reward data assets.*
