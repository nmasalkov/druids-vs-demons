# Battle scene: minimal playthrough checklist

Goal: prove the battle scene can actually be played end-to-end, not just that it compiles. Should
take roughly 10-30 seconds of real Play-mode time. Run every step in order — stop at the first one
that fails and report exactly which step and why, using logs/state you actually read, not a guess.

Use `mcp__coplay-mcp__execute_script` for anything not covered by a dedicated Coplay tool (clicking
UI buttons, reading private/runtime-only state), `get_unity_logs` after every step (not just at the
end) to catch errors as they happen, and `get_game_object_info` / `list_game_objects_in_hierarchy` to
confirm GameObject state directly instead of assuming it from code.

## Checklist

1. **Enter Play mode.** Wait out `GameStartState`'s ~2s intro delay before checking anything below.
2. **The player's slot machine becomes active.** Confirm `Canvas/SlotMachinePlayer` (or whichever
   side is currently active) is `IsActive: true` in the hierarchy. **This is the exact thing that
   silently failed once already** — `RollStateManager.ActivateSlotMachine()`'s `FightSO` gate
   returned early because `CampaignManager.CurrentEncounter` wasn't a fight (an empty/
   non-fight slot in `EncounterListSO`, or a stuck pick-screen ahead of the first fight). If it's not
   active, **stop — this is a blocker, not a warning**, and diagnose via
   `CampaignManager.Instance.CurrentEncounter`/`RunState.currentEncounterIndex` before doing
   anything else.
3. **A roll-type button is present and interactable** (e.g. `RollTypeButtonCreature`).
4. **Trigger a roll** (click/invoke the Start button).
5. **The roll finishes** — confirm `RollStateManager.OnRollFinished` actually fired: check
   `SpawnEntries`/`NukeEntries`/`SpellEntries` got populated, or that the slot machine deactivates
   again on its own.
6. **The enemy takes its turn** — `GameManager.ActiveSide` switches to `Enemy` and the enemy's slot
   machine activates (or is driven by `AIController`).
7. **A battle actually happens** — after both sides' turns, confirm `BattleState`/`AttacksResolver`
   ran: a log line, or an HP change on either side's creatures/hero.
8. **No errors anywhere in the window above** — read `get_unity_logs` across the whole sequence, not
   only at the end.

## Cleanup

Stop Play mode. If anything was saved during the test (a real reroll spend, a claimed reward, an
advanced encounter index), clear it via `SaveStorage.Backend.Delete()` so the next real session
starts clean, unless the test was specifically about save persistence.
