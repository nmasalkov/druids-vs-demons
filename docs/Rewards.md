# Rewards

## What this system does

Extends `RewardPickSO`/`RewardEncounter` (`docs/Encounters.md`) beyond a single guaranteed energy
grant: the player now also picks 1 of 3 randomly drawn reward cards. `RewardPickSO.energyReward` is
still granted automatically (now at `Play()`, not on Claim); the Claim button finalizes whichever
card was selected. Built on the `RewardSO` hierarchy (persisted by id in `RunState`, resolved via
`RewardListSO` — the same `ActionSO.id`/`GameCatalog.Find*(id)` pattern the loadout system already
uses), a draw/pooling algorithm (`RewardDrawer`), and a small stat-boost hook (`RewardBonuses`)
resolvers call into.

## `RewardSO` hierarchy

`Global/Campaign/Rewards/`:

- **`RewardSO`** (abstract `ScriptableObject`) — `id`, `rewardName`, `description`, `typeLabel`
  (card's type line, e.g. "Boost"), a nullable `icon` (`EffectiveIcon` falls back to
  `FallbackIcon`, overridden per subtype to borrow the linked action/creature's `cardSprite`),
  `unique` (bool — true = at most one copy can ever be owned, filtered from future draws once
  claimed). Abstract `Claim(RunState run)` and `IsOwned(RunState run)`.
- **`StatusRewardSO`** (abstract) — stacking, non-unique run modifiers. `IsOwned` always `false`.
  - **`HpBoostRewardSO`** — `bonusHp` (int, default 15). `Claim` appends `id` to
    `RunState.statusRewardIds` (duplicates allowed and expected — each claim stacks).
- **`BonusEnergyRewardSO`** — `bonusEnergy` (int, default 50). `Claim` is a pure
  `run.currentEnergy += bonusEnergy` — uncapped by `RunState.energyCapacity`, same as
  `RewardPickSO`'s guaranteed reward (see "Energy grants are uncapped" below). Not unique, not
  tracked in any list — a pure one-time effect.
- **`CreatureRewardSO`** — `creature` (`CreatureSO`). `Claim` appends `creature.id` to
  `RunState.gatheredCreatureIds`; `IsOwned` checks that same list. Unique. Not yet consumable —
  fielding a gathered creature into a loadout slot is a future follow-up (per the user request
  this was scoped from — "for now it is not used").
- **`BoostSO`** (abstract) — `action` (`ActionSO`), `percentage` (float, default `0.2`). `Claim`
  appends `id` to `RunState.boostRewardIds`; `IsOwned` checks that same list. Unique.
  - **`CreatureBoostRewardSO`** / **`NukeBoostRewardSO`** / **`SpellBoostRewardSO`** — empty
    bodies, exist purely so `RewardDrawer`/asset menus can type-discriminate, exactly like
    `ArcherSO`/`TankSO`/`MageSO` subclassing `CreatureSO` for the same reason.

## `RewardListSO`

`Global/Campaign/RewardListSO.cs` — top-level catalog, same shape as `GameCatalog`:
`List<RewardSO> allRewards` + `Find(string id)`. Asset:
`_ScriptableObjects/Campaign/RewardListSO.asset`. Exposed via the static `G.RewardList` accessor,
but — like `EncounterListSO`/`G.EncounterList` — the field itself is **not** serialized on `G`
(which is per-scene and doesn't exist in `MapScene`). It lives on `CampaignStateManager`
instead: `G.RewardList => CampaignStateManager.Instance.RewardList`. `CampaignStateManager`
lives on `_Prefabs/Campaign/CampaignProgress.prefab` (see `docs/Campaign.md`), instanced in
*both* `BattleScene.unity` and `MapScene.unity` (whichever scene boots first "wins" the session,
the other's copy self-destructs) — so wiring `rewardList` on the prefab wires both instances at
once. Before the prefab conversion this had to be set independently per scene and missing it on
one was a real, previously-hit bug: a save sitting on a reward-pick encounter, opened directly
into `MapScene` (skipping `BattleScene` for the session), NREs in `RewardDrawer.DrawThree` if that
scene's copy wasn't wired. The prefab conversion makes that class of bug structurally impossible
for this field going forward.

## `RunState` fields

```csharp
public List<string> statusRewardIds = new List<string>();     // claimed HpBoostRewardSO ids (dupes allowed)
public List<string> boostRewardIds = new List<string>();      // claimed BoostSO ids (unique)
public List<string> gatheredCreatureIds = new List<string>(); // unlocked CreatureSO ids (unique, via CreatureRewardSO)
```

The first list-shaped fields on `RunState` (every prior field was a scalar or single id string) —
`JsonUtility` supports `List<T>` member fields fine, this just hadn't come up before. Per CLAUDE.md
rule 21, each has a matching `List<T>` field on `CampaignProfileSO` (direct SO refs, mirroring the
scalar fields' convention) and a toggle+list override pair on `CampaignDebugTool` (applied in
`Awake()`, drawn via `CampaignDebugToolEditor`'s `SerializedProperty`-based `DrawToggleAndList` —
the existing `ref`-based `DrawToggleAndObject`/`DrawToggleAndInt` helpers don't fit a `List<T>`).

## Energy grants are uncapped

`RewardPickSO`'s guaranteed reward (`RewardEncounter.Play()`) and `BonusEnergyRewardSO.Claim()`
both do a plain `run.currentEnergy += reward` — **not** `Mathf.Min(current + reward,
energyCapacity)`. This used to clamp (a pre-existing behavior on the original single-reward
`RewardPickSO`, inherited by `BonusEnergyRewardSO` when it was added), which was a real, live-
caught bug: a player who'd spent reroll energy mid-battle (say down to 44/50) would win, get
granted the guaranteed reward, and see it silently eaten by the clamp (`44 + 20 = 64`, clamped
back down to `50` — indistinguishable from "the reward didn't apply" without reading the actual
arithmetic). Found via the `simple-debug` skill: logging every `RunState.currentEnergy` write site
and replaying the exact repro live showed the guaranteed-grant line was the one clamping. Fixed by
removing the clamp from both sites — `energyCapacity` is currently unenforced anywhere in
gameplay code (reserved for a possible future "capacity boost" reward), so reward grants simply
accumulate.

## Max HP integration

`CampaignStateManager.GetMaxHpBonus()` sums `HpBoostRewardSO.bonusHp` for every id in
`CurrentRun.statusRewardIds` (resolved via `G.RewardList.Find`); `CampaignStateManager.CurrentMaxHp =>
CurrentRun.maxHp + GetMaxHpBonus()`. `Hero.GetMaxHealth()`'s player branch reads `CurrentMaxHp`
instead of the raw `CurrentRun.maxHp` scalar — already re-evaluated fresh on every
`GameManager.OnBattleRestart` → `InitHealth()`, so no new event wiring was needed.

## `RewardBonuses` — the boost hook

```csharp
public static class RewardBonuses
{
    public static float ApplyBonuses(ActionSO action, float baseValue)
    {
        float bonus = 0f;
        foreach (var id in CampaignStateManager.Instance.CurrentRun.boostRewardIds)
            if (G.RewardList.Find(id) is BoostSO boost && boost.action == action)
                bonus += boost.percentage;
        return baseValue * (1f + bonus);
    }
}
```

Called explicitly at each resolver's balance-number read (not baked into the SOs' own accessor
methods — keeps `ScriptableObject`s pure data per CLAUDE.md rule 13):

- **Creature damage** — `AttacksResolver.Mechanics.cs`'s `ResolveTeam()`, wrapping `stats.damage`.
  One generic site, covers all 3 default creatures automatically.
- **Creature HP at spawn** — `Creature.GetMaxHealth()`, wrapping `Data.Stats(...).health`. This is
  the "HP bonus for creatures applies when spawning" case, via `CreatureBoostRewardSO` — distinct
  from `HpBoostRewardSO`, which boosts the player hero's max HP instead.
- **Nuke damage** — `FireMagicResolver`/`StarfallResolver`, each wrapping their own
  `source.GetDamageForLevel(level)` read.
- **Charm chance** — `CharmResolver`, wrapping `charmSO.GetChanceForLevel(level)` before the
  `* alive.Count` / `Clamp01`.
- **Battle Cry buff** — `BattleCryResolver`, wrapping `GetBuffMultiplierForLevel(level)` (only the
  buff side — the debuff dealt to the enemy stays unboosted).
- **Shield HP** — `Shield.Init(int level)`/`Shield.Promote(int level)`, wrapping
  `Data.GetHpForLevel(level)` (covers spawn, promote, *and* — via `ShieldResolver`'s
  `ShieldHealShot` — heal).

**Known gap: `ShockResolver` has no boostable stat.** Shock deals no damage, only applies the
Shocked status with a fixed duration — `Boost_Shock` (the 9th boost asset, created for full
per-default-action coverage) is currently inert until Shock gains a numeric stat worth boosting.

**Known gap shared with the loadout system (`docs/G.md`'s "No side flag anywhere" gotcha): boosts
apply to both sides identically.** `RewardBonuses.ApplyBonuses` matches purely by `ActionSO`
reference — since both sides already roll from the same shared `G.DefaultCreatures`/`DefaultNukes`/
`DefaultSpells` pool "by design," a boosted creature/nuke/spell is stronger whichever side rolls it.
Not a new limitation introduced here, just inherited from that existing, documented behavior.

## Card-draw algorithm — `RewardDrawer.DrawThree`

Pools, after excluding every `RewardSO` where `unique && IsOwned(run)`:

- **hpOrEnergy** = `HpBoostRewardSO` ∪ `BonusEnergyRewardSO` (never filtered — not unique, always
  has candidates)
- **creature** = unowned `CreatureRewardSO`
- **boost** = unowned `BoostSO` (all 3 subclasses)

Slot 1 is always drawn from `hpOrEnergy`. Slots 2 and 3 use a priority-with-cascade rule — try the
preferred pool, fall through to the next if it has no unclaimed candidates left:

- Slot 2: **creature → boost → hpOrEnergy**
- Slot 3: **boost → creature → hpOrEnergy**

A picked unique reward can't repeat within the same draw (tracked via a `used` set, checked in
`PickFrom`). This reproduces the exact behavior:

- Default draw: `[hpOrEnergy, creature, boost]`
- All creature rewards claimed: `[hpOrEnergy, boost, boost]`
- Creatures and boosts both claimed: `[hpOrEnergy, hpOrEnergy, hpOrEnergy]`
- Boosts claimed, creatures still available (the one case not explicit in the original spec,
  resolved by the same cascade in reverse): `[hpOrEnergy, creature, creature]`

## `RewardCard` / `RewardEncounter` flow

**`RewardEncounter` splits into a backend and a view (CLAUDE.md rule 28)**, so the whole pick can be
driven headlessly by test code with zero UI:

```csharp
public class RewardEncounter : Encounter
{
    public RewardPickSO Data { get; private set; }
    public IReadOnlyList<RewardSO> DrawnRewards { get; private set; }
    public RewardSO SelectedReward { get; private set; }

    public event Action<IReadOnlyList<RewardSO>> OnRewardsDrawn;
    public event Action<RewardSO> OnSelectionChanged;
    public event Action OnClaimed;

    public override void Play(EncounterSO data)
    {
        Data = (RewardPickSO)data;
        var run = CampaignStateManager.Instance.CurrentRun;
        run.currentEnergy += Data.energyReward;
        CampaignStateManager.Instance.Save();

        DrawnRewards = DrawRewards(run);
        OnRewardsDrawn?.Invoke(DrawnRewards);
    }

    private static List<RewardSO> DrawRewards(RunState run) =>
        DebugRewards.Instance != null
            ? DebugRewards.Instance.ConsumeOverrideDraw() ?? RewardDrawer.DrawThree(G.RewardList, run)
            : RewardDrawer.DrawThree(G.RewardList, run);

    public void SelectReward(RewardSO reward)
    {
        SelectedReward = reward;
        OnSelectionChanged?.Invoke(reward);
    }

    public void Confirm()
    {
        if (SelectedReward == null) return;
        var run = CampaignStateManager.Instance.CurrentRun;
        SelectedReward.Claim(run);
        CampaignStateManager.Instance.Save();
        OnClaimed?.Invoke();
        if (Headless) CompletePresentation();
    }
}
```

`RewardEncounter` has **no UI reference of any kind** — `Data`/`DrawnRewards`/`SelectedReward` are
the entire "menu" a test controller needs (`RewardSO` objects, not `RewardCard` visuals), and
`SelectReward`/`Confirm` are the entire "input" surface. `Confirm()`'s `RunState` mutation and
`Save()` always run unconditionally; only whether `Complete()` fires immediately (`Headless`) or
waits for the paired view depends on which mode it's running in — see CLAUDE.md rule 28.

`RewardCard` (`Global/Campaign/Rewards/RewardCard.cs`, prefab
`_Prefabs/UI/Cards/RewardCard.prefab`) — icon/name/type/description display, a single full-card
`Button` as the click target (clicking anywhere on the card selects it — there's no separate
sub-button), `Init(RewardSO)` (reads `EffectiveIcon`), `SetSelected(bool)` (delegates to the
sibling `RewardCardAnimator`'s `PlaySelect()`/`PlayDeselect()`), `event Action<RewardCard>
OnClicked`. No Claim button here — Claim lives once on `RewardEncounterView`, shared across all 3
spawned cards.

`RewardCardAnimator` (`Global/Campaign/Rewards/RewardCardAnimator.cs`, `[RequireComponent]`d by
`RewardCard`, same GameObject) owns all select/deselect/discard tweening via DOTween — same
pattern as `ShieldAnimator`. `PlaySelect()`/`PlayDeselect()` `DOScale` to `1.12`/`1` and
`DOAnchorPos` to base+`(0,15)`/base, 0.15s; `PlayDiscard(Action onComplete)` `DOScale`s to `0`
over 0.2s and invokes the callback (`RewardCard.PlayDiscard()` passes `() => Destroy(gameObject)`)
— no separate delayed-destroy call needed, DOTween's own `OnComplete` covers it.
**Every `Play*` call kills whatever tween is already running on this card first and always
animates toward an absolute fixed target** — critical for correctness: rapid re-selecting (or a
select interrupted by a deselect) can never leave two tweens racing or compound into a wrong
scale. This replaced an earlier `MMF_Player`/`MMF_Scale` version (still played through
`selectFeedback.PlayFeedbacks()`-style calls) that looked fine end-to-end but animated wrong in
practice — `MMF_Scale`'s `ToDestination` mode still applies `RemapCurveZero`/`RemapCurveOne` on
top of the already-lerped value, and those were left at their class defaults (`1`/`2`, meant for
`Absolute`/`Additive` mode) instead of `0`/`1`, so each play landed on a wrong intermediate scale
that the *next* play then used as its own starting point — compounding across repeated
select/deselect into a card visibly growing far past its intended size before snapping back.
`DiscardDuration` (`RewardCardAnimator.DiscardDuration`, a plain serialized field) is read by
`RewardEncounterView` the same way as before (rule 22 — one source of truth, no duplicated magic
number).

**`RewardEncounter` (backend) `Play(EncounterSO data)`:**

1. Grants the guaranteed energy immediately (`run.currentEnergy += energyReward`, uncapped — see
   "Energy grants are uncapped" below), saves.
2. Draws via `DrawRewards()` (`DebugRewards.ConsumeOverrideDraw() ?? RewardDrawer.DrawThree(...)`),
   sets `DrawnRewards`, fires `OnRewardsDrawn`.

**`RewardEncounterView` reacts to that event:** sets `messageText.text = "You got {N} energy!"`,
spawns one `RewardCard` per `cardSlots` anchor (see CLAUDE.md's anchor+disabled-template rule) —
destroying whatever's currently parented under each anchor (the disabled placeholder card, or a
leftover from a previous draw) and instantiating the real `RewardCard` as its child, subscribing to
`OnClicked` — and sets `claimButton.interactable = false` until a card is selected.

Clicking a card calls `_backend.SelectReward(card.Data)`; the backend fires `OnSelectionChanged`,
which the view reacts to by deselecting the previous card, selecting the new one, and enabling
Claim. Clicking Claim calls `_backend.Confirm()`: `claimButton.interactable = false`, claims the
selected card's `RewardSO.Claim(run)`, saves, fires `OnClaimed` — which the view reacts to by
calling `PlayDiscard()` on every other spawned card (the claimed card just stays as-is) and delaying
`_backend.CompletePresentation()` by the longest `DiscardDuration` among them via `Utils.DoAfterDelay`
— so the shrink-and-destroy animation is visible instead of getting cut off by the encounter-complete
transition. Only `Confirm()` ever advances it — unlike LoadoutEncounter, a stray selection change
alone must not grant a reward early. A test controller can skip all of this UI entirely: set
`Headless = true` before `Play()`, then call `SelectReward`/`Confirm` directly — `Confirm()`
self-completes immediately instead of waiting on a view. See CLAUDE.md rule 28.

## Debugging: `DebugRewards` and `RunStateMonitor`

`Global/Campaign/DebugRewards.cs` — a component on `CampaignProgress.prefab` alongside
`CampaignManager`/`CampaignStateManager`, same cross-scene duplicate-guard singleton pattern. Check
`rollOnNextReward` and assign `slot1`/`slot2`/`slot3` to force exactly those 3 `RewardSO`s on the
next `RewardEncounter` instead of a random draw — the override auto-clears once consumed (see
CLAUDE.md rule 25, and rule 26 for why this had to be a root-level GameObject component, not
nested). `RewardEncounter.DrawRewards()` checks `DebugRewards.Instance?.ConsumeOverrideDraw()`
before ever calling `RewardDrawer.DrawThree` — the one hijack point, nothing upstream/downstream
touched.

Full `RunState` visibility (every field, including `statusRewardIds`/`boostRewardIds`/
`gatheredCreatureIds`) is a `RunStateMonitor` child GameObject under `CampaignProgress.prefab` —
see `docs/Campaign.md`. It replaced an earlier `CampaignManagerEditor` "Gathered Rewards"
section (raw ids plus a resolve-to-assets button) that only covered the reward-specific fields;
`RunStateMonitor` shows the whole `RunState` and needs no button since it's opt-in via its own
`enabled` checkbox rather than resolved on demand.

## Editor setup checklist

- **`RewardCard.prefab`'s animation is `RewardCardAnimator` (DOTween), not `MMF_Player`s** —
  auto-added via `[RequireComponent]`, default field values (`1.12` select scale, `15` move-up,
  `0.15s`/`0.2s` durations) are already tuned, nothing to wire manually.
- **`HpBoostRewardSO`/`BonusEnergyRewardSO` have no icon assigned** — no linked `ActionSO` to
  borrow `cardSprite` from (`FallbackIcon` stays `null` for these two), so they render with a
  blank icon until placeholder art is assigned in the Inspector.
- **All 14 reward assets live in `_ScriptableObjects/Campaign/Rewards/`**, referenced by
  `RewardListSO.asset`. The 3 "Big" creature variants (`BubkaBig`/`TankBig`/`DragonBig`, ids
  `archer_big`/`tank_big`/`mage_big`, 1.5x prefab scale) back the 3 `CreatureRewardSO`s and are
  registered in `GameCatalog.asset`'s `allCreatures` alongside the originals.
- **`RewardEncounter.prefab`'s root GameObject carries both a `RewardEncounter` (backend) and a
  `RewardEncounterView` (UI) component** (CLAUDE.md rule 28). `cardSlots` (`CardSlot1`/`CardSlot2`/
  `CardSlot3`, each holding a disabled placeholder `RewardCard` for Inspector debugging — CLAUDE.md's
  anchor+disabled-template rule), `cardPrefab`, `messageText`, and `claimButton` are all fields on
  `RewardEncounterView`, not `RewardEncounter` — `RewardEncounter` itself has no serialized UI fields
  at all. `claimButton` starts non-interactable by default in the prefab itself (also enforced in
  code at `HandleRewardsDrawn()`).
- **`CampaignStateManager`'s `rewardList` field is wired on `CampaignProgress.prefab`** — since
  it's a real prefab now (see `docs/Campaign.md`), this only needs setting once; both scene
  instances pick it up automatically. `slot1`/`slot2`/`slot3` on `DebugRewards` are left
  unassigned by default, assign per-debug-session in the Inspector (on either scene instance —
  they're per-instance fields, not shared) as needed.

## Related docs

- `docs/Encounters.md` — `RewardPickSO`/`RewardEncounter`'s original single-guaranteed-reward
  shape this system extends, and the `Encounter`/`EncounterPlayer`/`MapManager` dispatch that
  instantiates it.
- `docs/Campaign.md` — `RunState`, `CampaignProfileSO`, `CampaignDebugTool`, and the `ActionSO.id`/
  `GameCatalog` id-resolution pattern `RewardSO.id`/`RewardListSO` mirrors.
- `docs/ActionsAndSpells.md`, `docs/Battle.md` — the resolvers `RewardBonuses.ApplyBonuses` is
  called from.
