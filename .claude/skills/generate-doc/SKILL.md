---
name: generate-doc
description: Regenerates docs/GameDesign.md — a single, zero-implementation-detail description of the whole game (every mechanic, rule, process, and current balance number) compiled from every docs/*.md file plus live balance data pulled from the game's data assets. Use whenever the user asks for "the game design doc," a rules/mechanics reference for an LLM or game designer, or explicitly runs /generate-doc — and after any change that alters game mechanics/balance and should be reflected there.
---

# Generate Game Design Doc

Produces `docs/GameDesign.md`: everything a player actually experiences — mechanics, rules, numbers,
processes — with **zero implementation/code/architecture content** (no class names, no
`ScriptableObject`/`MonoBehaviour`, no event names, no file paths, no C#). It reads like a game
designer's bible or an LLM's "what does this game do" briefing, not like the engineering docs it's
built from. Audience: a game designer, or an LLM that needs the full ruleset without needing to know
how any of it is coded.

This is a **compile + translate** task, not a summarize task: every technical `docs/*.md` file
describes the same mechanics `GameDesign.md` needs, just from an implementation angle. The job is to
re-derive the player-facing behavior from that technical description and drop everything about *how*
it's built.

## 1. Read every technical doc

`ls docs/` (or `Glob docs/*.md`) and read **every** file except `GameDesign.md` itself — don't work
from memory of what these docs said last time, they may have changed. As of this skill's authoring
the set is: `AI.md`, `ActionsAndSpells.md`, `Battle.md`, `Campaign.md`, `Encounters.md`, `Energy.md`,
`Experience.md`, `G.md`, `GameLoop.md`, `Loadout.md`, `Rewards.md`, `SlotMachine.md` — but the actual
directory listing is the source of truth, not this list (a new system may have gained its own doc
since).

## 2. Pull live balance numbers from Unity

The technical docs describe *formulas and mechanics* but rarely the actual tuned numbers (damage,
HP, costs, chances) — those live on data assets in the project, not in any doc. Pull them live via
Coplay's `execute_script` rather than hand-decoding the assets' on-disk YAML (their numeric arrays
are packed/binary-encoded in the raw file and are not meant to be hand-read).

Write a throwaway C# script to the scratchpad directory and run it with `execute_script`. Two gotchas
that will otherwise burn a retry cycle:

- **No `using Game._Scripts...;` namespace imports** — most of this project's gameplay classes
  (`CreatureSO`, `NukeSO`, action resolvers, etc.) live in the **global namespace**, not a
  `Game._Scripts` namespace. Adding such a `using` is itself a compile error. If `execute_script`
  comes back with an opaque `Microsoft.CodeAnalysis` resource-loading exception instead of a normal
  compiler error message, that's this tool's way of saying "your script didn't compile" — the
  message itself won't tell you why. Isolate by first running a trivial one-line script; once that
  succeeds, the failure is in your script's own code, most likely a bad `using`.
- **Reflection must walk the type hierarchy for inherited private fields.** Concrete SOs like
  `FireMagicSO`/`ShockSO` subclass a base (`NukeSO`) that actually declares the balance fields
  (`damagePerLevel`, `ignoresShield`, ...) as non-public `[SerializeField]`s. A plain
  `so.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)` only returns fields
  **declared on that exact type**, silently skipping every non-public field declared on a base
  class — you'll get back the subclass's own `id`/`actionName`/etc. but nothing from `NukeSO`
  itself. Walk `t.BaseType` up the chain collecting each level's fields instead of reflecting on the
  leaf type alone. Same applies to `CreatureSO` subclasses (`ArcherSO`/`TankSO`/`MageSO`) and
  `SpellSO` subclasses.
- Struct-typed fields (e.g. a fight's enemy-data block) won't usefully print via a generic
  "enumerate and `ToString()`" dumper — reflect into the struct's own fields by name instead
  (`GetVal(structInstance, "hp")`, etc.), the same way you'd reflect into the containing object.
- Set `CultureInfo.CurrentCulture = CultureInfo.InvariantCulture` before formatting any floats —
  otherwise decimal points can render as commas depending on the Editor machine's locale and corrupt
  every percentage/multiplier you pull.

Pull at minimum: every creature's per-level damage/HP/attack-count, XP thresholds/rewards/heal
amounts; every Nuke's and Spell's per-level numbers; every fight's enemy HP/roster/AI tuning
(stupidity/critical-failure/reroll budget)/reward amount/loadout-pick flag; every reward card's
effect size; starting run stats (starting energy, base reroll cost, starting Max HP, starting
loadout). Write results to a scratchpad text file and read it back rather than trying to capture a
huge return value directly.

If Coplay MCP isn't reachable for some reason, fall back to reading the relevant `.asset` files
directly — the small integer/float fields (single values, not packed arrays) are readable in the raw
YAML; only Unity-serialized array/list fields are opaque and need Unity itself (or a script run) to
decode reliably. Don't invent numbers — if a value genuinely can't be recovered, mark it
`(not available)` in the doc rather than guessing.

## 3. Translate — the zero-implementation rule

This is the part that actually takes judgment. For every mechanic in every technical doc, ask "what
does a player/designer observe or need to know," and write *that*, discarding everything about how
it's built. Concretely, never let any of the following leak into `GameDesign.md`:

- Class/type/method/field/event names (`RollState`, `AttacksResolver`, `OnBattleRestart`,
  `IsShocked`, ...) — describe the behavior they implement instead ("a stunned creature skips its
  attack until healed, promoted, or killed").
- Engine/architecture vocabulary (`ScriptableObject`, `MonoBehaviour`, `singleton`, `coroutine`,
  `serialized field`, `Inspector`, `prefab`, ...).
- File paths, asset paths, folder structure.
- Anything framed as "how this is tested/debugged/restarted internally" (staleness guards, instant-
  resolve paths, debug tools, save-system internals) — a player never sees any of this. The
  exception is genuine player-facing persistence behavior ("progress autosaves," "a restart refunds
  energy spent this attempt") — keep the *behavior*, drop the *mechanism*.
- Any note about *why* the code is shaped a certain way (a past bug, a refactor rationale, a rule
  number from `CLAUDE.md`) — irrelevant to what the game does.

Do keep, translated into plain language: every rule, formula, priority order, threshold, timing
relationship ("X only happens after Y"), and edge case that changes what a player experiences —
these are exactly the parts of the technical docs worth carrying over, just re-worded without the
implementation frame. When a doc flags something as a placeholder, an inert/unused field, or a
"known gap," that's genuinely useful to a designer too — carry it into a "current scope / known
gaps" section rather than silently dropping it, but phrase it as a design-scope note ("this reward
card has no effect yet"), not as an engineering gotcha.

## 4. Structure

Match the shape already established in `docs/GameDesign.md` (regenerate it wholesale rather than
patching — full rewrite, not an incremental diff): an elevator pitch, session/run structure, the
core round-and-turn loop, each major system as its own section (slot machine, creatures, combat,
nukes, spells, heroes/health/win-condition, energy, enemy AI, campaign/map structure, pre-battle
loadout, post-battle rewards, what persists across a run), a numbers-heavy balance reference section
(tables: creature stats by level, nuke/spell numbers by match level, fight-by-fight progression,
reward cards), and a closing "current scope & known gaps" section. Add a new top-level section for
any genuinely new system a future doc introduces that doesn't fit an existing section — don't force
it into an unrelated one.

Use tables for anything numeric/tabular (creature stats, fight progression, reward list) — far more
scannable than prose for a designer cross-referencing numbers. Keep prose sections tight: this
should read like a rulebook, not a narrative walkthrough.

## 5. Write and report

Overwrite `docs/GameDesign.md` in full. In the final line of the file, keep (and update if the
source list changed) the "compiled from" attribution note pointing back at the technical docs, so a
reader knows where to go for implementation detail.

Report back briefly: which technical docs were read, whether live balance data was pulled
successfully (and from how many assets), and call out anything you had to mark `(not available)` or
anything you noticed in the technical docs that looked stale/contradictory (e.g. two docs disagreeing
about the same number) so the user can reconcile it at the source.

## Related

- `docs/GameDesign.md` — the artifact this skill produces.
- `CLAUDE.md`'s Documentation map — the current list of technical `docs/*.md` files and what each
  covers; check it for any doc added since this skill was last updated.
