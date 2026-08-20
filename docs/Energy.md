# Reroll Energy

## What this system does

The player spends "energy" to reroll a slot column's result during `PostRolls`. The reroll cost
starts cheap and doubles with every successful reroll, resetting back to the base cost whenever a
roll phase ends. `EnergyController` is the singleton that owns both the player's total energy pool
and the current per-reroll cost; `SlotColumn` reads it to gate/display rerolls, and `EnergyDisplay`
mirrors the total onto the battle HUD.

**The energy pool has exactly one source of truth: `RunState.currentEnergy`** (see `docs/Campaign.md`
and `docs/Encounters.md`). `EnergyController.CurrentEnergy` is a computed read-through property
(`=> CampaignStateManager.Instance.CurrentRun.currentEnergy`), never a locally cached field — every
spend/revert/grant writes straight to `RunState.currentEnergy` and the property just reflects
whatever's there. This is a deliberate single-source-of-truth policy (see CLAUDE.md): storing the
same number in two places invites them silently drifting apart, so nothing but `RunState` itself
ever "holds" the value — everything else reads it live and reacts to `OnEnergyChanged` for UI
purposes. Only `CurrentRerollCost`'s reset value (`baseRerollCost`) is a plain local field, since
it's genuinely `EnergyController`-owned state with no campaign-persistence concern.

## Key files

- `Assets/Game/_Scripts/Global/GameManager/EnergyController.cs` — singleton; owns
  `CurrentRerollCost`, `TrySpendReroll()`, cost-reset-per-roll-phase, and the battle-restart
  revert. Exposes `CurrentEnergy` as a read-through onto `RunState.currentEnergy` — see above.
  Lives on the `Global/GameManager` GameObject in `BattleScene.unity`, alongside `GameManager`,
  `RollStateManager`, `ExperienceManager`, etc. — reads `CampaignStateManager`/`CampaignManager`
  (both cross-scene-persistent on the separate `CampaignProgress` prefab, not co-located here — see
  `docs/Campaign.md`) for `RunState`.
- `Assets/Game/_Scripts/SlotMachine/SlotColumn.cs` — gates `OnRerollClicked` on
  `EnergyController.Instance.TrySpendReroll()`, shows the per-slot reroll cost
  (`EnergyCount/EnergyQuantityText` in `Slot.prefab`), plays a feedback on cost change, and disables
  the reroll button when the player can't afford it.
- `Assets/Game/_Scripts/UI/EnergyDisplay.cs` — shows `EnergyController.CurrentEnergy` on the
  `Canvas/EnergyCount` HUD object in `BattleScene.unity` and plays a feedback whenever it changes.

## Cost model

- `baseRerollCost` (default 2) is the same cost for every column — there's a single global reroll
  price, not one per slot.
- `TrySpendReroll()`: if `CurrentEnergy >= CurrentRerollCost`, writes
  `CampaignStateManager.Instance.CurrentRun.currentEnergy = CurrentEnergy - CurrentRerollCost` directly
  (in-memory only, no `Save()` call — see "Restart interaction" below and `docs/Campaign.md`'s Save
  system section for when it actually reaches storage), firing `OnEnergyChanged`, then doubles
  `CurrentRerollCost` (firing `OnRerollCostChanged`) for the *next* reroll. Returns `false` and
  changes nothing if the player can't afford it — `SlotColumn.OnRerollClicked` treats that as a
  no-op.
- The cost resets back to `baseRerollCost` on `RollStateManager.Instance.OnRollFinished` — i.e.
  whenever a roll phase (`RollState`) ends, for either side. The AI does reroll, but through its own
  fight-wide reroll pool (`docs/AI.md`), never through `EnergyController` — so resetting the player's
  cost unconditionally on both sides' roll-finish is still harmless and avoids tracking whose turn
  it was.

## UI wiring

Both `Slot.prefab`'s per-slot `EnergyCount` (icon + `EnergyQuantityText`) and `BattleScene`'s HUD
`Canvas/EnergyCount` (icon + `EnergyQuantityText`) carry an `MMF_Player` with two feedbacks played
together on change (`MMF_Position` in `AlongCurve` mode for a short shake, `MMF_Scale` for a yoyo
punch) — both are self-contained tween feedbacks (no `MMPositionShaker` companion component
needed), per the project's rule to play all game-feel effects through Feel feedbacks rather than
driving transforms directly.

- `SlotColumn.rerollCostChangeFeedback` plays on `EnergyController.OnRerollCostChanged` — every
  column plays its own local copy since the cost is shared but each column owns its own HUD
  subtree.
- `EnergyDisplay.energyChangeFeedback` plays on `EnergyController.OnEnergySpent` (**not**
  `OnEnergyChanged`) — see "Two energy events" below.

## Restart interaction

Two different things both currently fire through the exact same `GameManager.RestartBattle()` →
`OnBattleRestart` mechanism (see `docs/GameLoop.md`), but need opposite energy behavior:

- **A real encounter transition** (victory → next encounter, a claimed reward via `FightSO.hasReward`,
  ...) should **carry the spend over** — if you fought a battle down to 5 energy, the next encounter
  starts at 5.
- **A same-encounter restart** (pause menu "Restart", a defeat retry) should **revert** any reroll
  spend from the abandoned attempt — you get back exactly what you had when *this* encounter began,
  not a free top-up, but also not permanently punished for retrying.

`EnergyController` resolves this with a snapshot, `_encounterStartEnergy`, rather than by having
`OnBattleRestart`'s handler try to distinguish the two cases (it structurally can't — both look
identical from inside the handler):

- `ApplyCampaignEnergy()` — called by `CampaignStateManager.ApplyEncounterToScene()` whenever
  `BattleScene` is entered (boot, or every later load/soft transition) — captures
  `_encounterStartEnergy = CurrentEnergy` and fires `OnEnergyChanged`. This always runs *before*
  the transition's own `GameManager.RestartBattle()` (see `CampaignManager.
  LoadCurrentEncounter()`), so by the time `OnBattleRestart` fires, the snapshot already reflects
  the new encounter's carried-over starting value.
- `ResetForRestart()` (the `OnBattleRestart` handler) writes `_encounterStartEnergy` back into
  `RunState.currentEnergy` and resets `CurrentRerollCost` to `baseRerollCost`, firing both change
  events.
- **On a same-encounter restart**, `ApplyCampaignEnergy()` is *not* called again first — the
  snapshot is still whatever it was when this encounter actually began, so `ResetForRestart()`
  performs a genuine revert.
- **On a real transition**, `ApplyCampaignEnergy()` *is* called again first (with the just-spent,
  carried-over value), refreshing the snapshot to match — so `ResetForRestart()`'s "revert" is a
  no-op against an already-correct number.

One `OnBattleRestart` subscription, no special-casing of *which* restart this is — the snapshot
being refreshed (or not) just beforehand is what makes the single handler correct for both cases.

## Two energy events: `OnEnergyChanged` vs `OnEnergySpent`

`CurrentEnergy` changes for two very different reasons, and the HUD should only visibly react
("shake") to one of them:

- **`OnEnergyChanged(int)`** fires on *every* change to `CurrentEnergy` — a real reroll spend
  (`TrySpendReroll`), a fresh campaign value being applied (`ApplyCampaignEnergy`, i.e. initial load
  or an encounter transition), or a restart reverting to the encounter-start snapshot
  (`ResetForRestart`, see "Restart interaction" above). Used for anything that must always reflect
  the true current number: `EnergyDisplay`'s text, `SlotColumn.RefreshRerollInteractable`.
- **`OnEnergySpent()`** fires *only* from `TrySpendReroll()` — the player actually spending energy
  on a reroll. `EnergyDisplay.energyChangeFeedback` (the HUD shake/scale punch) subscribes to this
  one, not `OnEnergyChanged`.

Before this split, `EnergyDisplay` played its feedback on every `OnEnergyChanged` fire, including
campaign-driven ones — caught live: loading a new encounter (`ApplyCampaignEnergy`) immediately
followed by that same transition's `GameManager.RestartBattle()` (→ `ResetForRestart`) fired
`OnEnergyChanged` twice in quick succession before a battle had even started, playing the shake
feedback twice back-to-back. The `MMF_Player`'s `MMF_Scale` punch (`CanPlayWhileAlreadyPlaying: true`)
retriggered mid-tween, so the second play started from the not-yet-fully-returned mid-punch scale
instead of the true baseline — visibly shrinking the HUD icon a little more each stack instead of
returning to its original size. Restricting the feedback to `OnEnergySpent` fixes both symptoms at
once: it no longer plays before a battle starts, and it can never double-fire from a single
encounter transition since only one real reroll spend happens at a time.

## Gotchas

- `EnergyController.Start()` subscribes to `RollStateManager.Instance.OnRollFinished` — this relies
  on `RollStateManager.Awake()` having already run (Unity runs every `Awake()` in the scene before
  any `Start()`), not on GameObject/component ordering. Both singletons happen to live on the same
  `Global/GameManager` GameObject today, but the dependency is on Unity's Awake/Start phasing, not
  co-location.
- The reroll-button interactable state (`SlotColumn.RefreshRerollInteractable`) also listens to
  `OnEnergyChanged`, not just `OnRerollCostChanged` — spending energy can make the *next* reroll
  unaffordable even though the cost itself didn't just change on this event.
- **`CurrentEnergy` needs no init of its own** — being a read-through onto
  `CampaignStateManager.Instance.CurrentRun.currentEnergy`, it's already correct the instant
  `CampaignStateManager.Awake()` creates `CurrentRun`, before any `Start()` runs anywhere (rule 5 —
  `CampaignStateManager` is cross-scene-persistent, already alive by the time `BattleScene` finishes
  loading). Unlike before this was a read-through property, there's
  no window where it shows a stale/default value while waiting for `ApplyCampaignEnergy()` — that
  call now only matters for taking the `_encounterStartEnergy` snapshot and firing `OnEnergyChanged`
  for the UI, not for making `CurrentEnergy` itself correct.
