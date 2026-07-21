# Reroll Energy

## What this system does

The player spends "energy" to reroll a slot column's result during `PostRolls`. The reroll cost
starts cheap and doubles with every successful reroll, resetting back to the base cost whenever a
roll phase ends. `EnergyController` is the singleton that owns both the player's total energy pool
and the current per-reroll cost; `SlotColumn` reads it to gate/display rerolls, and `EnergyDisplay`
mirrors the total onto the battle HUD.

**The energy pool itself is campaign-persistent** (`RunState.currentEnergy`, see `docs/Campaign.md`
and `docs/Encounters.md`) — `EnergyController` has no local baseline of its own anymore. Only
`CurrentRerollCost`'s reset value (`baseRerollCost`) is still a plain local field.

## Key files

- `Assets/Game/_Scripts/Global/GameManager/EnergyController.cs` — singleton; owns `CurrentEnergy`/
  `CurrentRerollCost`, `TrySpendReroll()`, cost-reset-per-roll-phase, and the battle-restart reset.
  Lives on the `Global/GameManager` GameObject in `BattleScene.unity`, alongside `GameManager`,
  `RollStateManager`, `ExperienceManager`, `CampaignManager`, etc. — the last of which fully owns
  what `CurrentEnergy` gets set to; see Restart interaction below.
- `Assets/Game/_Scripts/SlotMachine/SlotColumn.cs` — gates `OnRerollClicked` on
  `EnergyController.Instance.TrySpendReroll()`, shows the per-slot reroll cost
  (`EnergyCount/EnergyQuantityText` in `Slot.prefab`), plays a feedback on cost change, and disables
  the reroll button when the player can't afford it.
- `Assets/Game/_Scripts/UI/EnergyDisplay.cs` — shows `EnergyController.CurrentEnergy` on the
  `Canvas/EnergyCount` HUD object in `BattleScene.unity` and plays a feedback whenever it changes.

## Cost model

- `baseRerollCost` (default 2) is the same cost for every column — there's a single global reroll
  price, not one per slot.
- `TrySpendReroll()`: if `CurrentEnergy >= CurrentRerollCost`, deducts `CurrentRerollCost` from
  `CurrentEnergy` (firing `OnEnergyChanged`), then doubles `CurrentRerollCost` (firing
  `OnRerollCostChanged`) for the *next* reroll. Returns `false` and changes nothing if the player
  can't afford it — `SlotColumn.OnRerollClicked` treats that as a no-op.
- The cost resets back to `baseRerollCost` on `RollStateManager.Instance.OnRollFinished` — i.e.
  whenever a roll phase (`RollState`) ends, for either side. Only the player ever actually rerolls
  today (`AIController`'s reroll decision is a stub, see `docs/SlotMachine.md`), so resetting
  unconditionally on both sides' roll-finish is harmless and avoids tracking whose turn it was.
- There's no local "starting energy" anymore — `CurrentEnergy` is set exclusively by
  `CampaignManager` from `RunState.currentEnergy` (see Restart interaction below). Energy now
  *does* carry over between battles (a campaign concern, `docs/Encounters.md`), not just between
  rounds of the same battle.

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

`EnergyController` subscribes to `GameManager.OnBattleRestart` in `Start()` (unsubscribing in
`OnDestroy()`), per the project-wide restart convention (see `docs/GameLoop.md`).
`ResetForRestart()` refills `CurrentEnergy` to **`CampaignManager.Instance.CurrentRun.currentEnergy`,
read fresh** (not a cached field) and resets `CurrentRerollCost` to `baseRerollCost`, firing both
change events so the UI updates immediately.

Reading `RunState.currentEnergy` fresh on every restart (rather than caching a per-battle baseline)
is exactly what makes restarting the *current* encounter restore pre-battle energy correctly: that
field only ever changes via `CampaignProgressManager.ResolveVictory()`'s reward grant — never during
a restart, and never mid-battle (spending only touches the live `CurrentEnergy`, not `RunState`) —
so a restart always resets to "what you had when this encounter started." See docs/Encounters.md's
"Handle Battle Restart Correctly" for the full requirement this satisfies.

`CampaignManager.ApplyEncounterToScene()` is the other place `CurrentEnergy` gets set (initial scene
load, and every encounter transition, soft or full reload) — see `docs/Campaign.md`.

## Two energy events: `OnEnergyChanged` vs `OnEnergySpent`

`CurrentEnergy` changes for two very different reasons, and the HUD should only visibly react
("shake") to one of them:

- **`OnEnergyChanged(int)`** fires on *every* change to `CurrentEnergy` — a real reroll spend
  (`TrySpendReroll`), a fresh campaign value being applied (`ApplyCampaignEnergy`, i.e. initial load
  or an encounter transition), or a restart reverting to the pre-battle value (`ResetForRestart`).
  Used for anything that must always reflect the true current number: `EnergyDisplay`'s text,
  `SlotColumn.RefreshRerollInteractable`.
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
- **`CurrentEnergy` is left at its C# default (`0`) between `Awake()` and whenever
  `CampaignManager` first calls `ApplyCampaignEnergy()`** — safe today only because
  `CampaignManager.Start()` has Script Execution Order `-100`, guaranteed to run before this
  component's own `Start()` or any consumer's (`EnergyDisplay`, `SlotColumn`). If `EnergyController`
  is ever used in a scene without `CampaignManager`, it will show `0` energy until something applies
  a real value.
