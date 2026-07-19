# Reroll Energy

## What this system does

The player spends "energy" to reroll a slot column's result during `PostRolls`. The reroll cost
starts cheap and doubles with every successful reroll, resetting back to the base cost whenever a
roll phase ends. `EnergyController` is the singleton that owns both the player's total energy pool
and the current per-reroll cost; `SlotColumn` reads it to gate/display rerolls, and `EnergyDisplay`
mirrors the total onto the battle HUD.

## Key files

- `Assets/Game/_Scripts/Global/GameManager/EnergyController.cs` — singleton; owns `CurrentEnergy`/
  `CurrentRerollCost`, `TrySpendReroll()`, cost-reset-per-roll-phase, and the battle-restart reset.
  Lives on the `Global/GameManager` GameObject in `BattleScene.unity`, alongside `GameManager`,
  `RollStateManager`, `ExperienceManager`, etc.
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
- `startingEnergy` (default 50) is granted once at `Start()` and refilled on battle restart —
  energy does not carry over between separate battles, only between rounds of the same battle.

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
- `EnergyDisplay.energyChangeFeedback` plays on `EnergyController.OnEnergyChanged`.

## Restart interaction

`EnergyController` subscribes to `GameManager.OnBattleRestart` in `Start()` (unsubscribing in
`OnDestroy()`), per the project-wide restart convention (see `docs/GameLoop.md`).
`ResetForRestart()` refills `CurrentEnergy` to `startingEnergy` and resets `CurrentRerollCost` to
`baseRerollCost`, firing both change events so the UI updates immediately.

## Gotchas

- `EnergyController.Start()` subscribes to `RollStateManager.Instance.OnRollFinished` — this relies
  on `RollStateManager.Awake()` having already run (Unity runs every `Awake()` in the scene before
  any `Start()`), not on GameObject/component ordering. Both singletons happen to live on the same
  `Global/GameManager` GameObject today, but the dependency is on Unity's Awake/Start phasing, not
  co-location.
- The reroll-button interactable state (`SlotColumn.RefreshRerollInteractable`) also listens to
  `OnEnergyChanged`, not just `OnRerollCostChanged` — spending energy can make the *next* reroll
  unaffordable even though the cost itself didn't just change on this event.
