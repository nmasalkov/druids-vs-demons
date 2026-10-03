# Rewards

## What this system does

Extends `FightSO.hasReward`/`RewardEncounter` (`docs/Encounters.md`) beyond a single guaranteed energy
grant: the player now also picks 1 of 3 randomly drawn reward cards. `FightSO.rewardAmount` is
granted automatically at `Play()` (not on Claim); the Claim button finalizes whichever card was
selected. Shown as an overlay directly inside `BattleScene` right after victory (not `MapScene` —
see `docs/Encounters.md`'s "Encounter dispatch"), dispatched by `BattleRewardPresenter`. Built on the
`RewardSO` hierarchy (persisted by id in `RunState`, resolved via `RewardListSO` — the same
`ActionSO.id`/`GameCatalog.Find*(id)` pattern the loadout system already uses), a draw/pooling
algorithm (`RewardDrawer`), and a small stat-boost hook (`RewardBonuses`) resolvers call into.

## `RewardSO` hierarchy

`_Global/_Campaign/_Rewards/`:

- **`RewardSO`** (abstract `ScriptableObject`) — `id`, `rewardName`, `description`, `typeLabel`
  (card's type line, e.g. "Boost"), a nullable `icon` (`EffectiveIcon` falls back to
  `FallbackIcon`, overridden per subtype to borrow the linked action/creature's `cardSprite`),
  `unique` (bool — true = at most one copy can ever be owned, filtered from future draws once
  claimed). Abstract `Claim(RunState run)` and `IsOwned(RunState run)`.
- **`StatusRewardSO`** (abstract) — stacking, non-unique run modifiers. `IsOwned` always `false`.
  - **`HpBoostRewardSO`** — `bonusHp` (int, default 15). `Claim` appends `id` to
    `RunState.statusRewardIds` (duplicates allowed and expected — each claim stacks).
- **`BonusEnergyRewardSO`** — `bonusEnergy` (int; code default 50, the live `bonusenergy_50`
  asset grants **20** despite its filename). `Claim` is a pure
  `run.currentEnergy += bonusEnergy` — uncapped by `RunState.energyCapacity`, same as
  a fight's guaranteed `FightSO.rewardAmount` (see "Energy grants are uncapped" below). Not unique,
  not tracked in any list — a pure one-time effect.
- **`CreatureRewardSO`** — `creature` (`CreatureSO`). `Claim` appends `creature.id` to
  `RunState.gatheredCreatureIds`; `IsOwned` checks that same list. Unique. A gathered creature
  becomes pickable in the next pre-battle loadout picker (`LoadoutPickEncounter` reads
  `gatheredCreatureIds`, `docs/Loadout.md`). Four creature rewards exist:
  - `creature_bubka_big` (`BubkaBig`) has stats identical to its base creature (a Phase 2
    follow-up).
  - `creature_lizard` unlocks **Lizard** (plain `MageSO`, id `lizard`), a glass-cannon mage: 70% of
    Dragon's HP, +28–32% base damage, and a 35% / +100% critical strike (`docs/Battle.md`
    "Critical strike") — about 1.22x Dragon by expected damage x HP. Fires `_AcidSpit`. Lizard
    replaced the old `DragonBig` placeholder (id `mage_big`), whose assets were deleted; a save
    still holding `mage_big` falls back to the default mage in `ApplyLoadoutToG`.
  - `creature_bulba` unlocks **Bulba** (`CounterTankSO`, id `bulba`), a tank that never attacks,
    taunts (`preferredTarget`, enemy creature attacks hit it first), and counterattacks every hit
    on it (`docs/Battle.md` "Counterattacks"). Bulba replaced the old
    `TankBig` placeholder, whose assets were deleted.
  - `creature_healing_shroom` unlocks **Healing Shroom** (`HealingArcherSO`, id `healing_shroom`).
    It is a frail archer that, before each battle, fires a volley of heals at random wounded allies (never itself), then attacks normally (`docs/Battle.md`
    "Healing shots").
- **`BoostSO`** (abstract) — unique run-long upgrades. `Claim` appends `id` to
  `RunState.boostRewardIds`; `IsOwned` checks that same list. **Every boost applies to the player
  side only** — see "`RewardBonuses`" below. Three concrete shapes:
  - **`CreatureClassBoostSO`** — nested `enum CreatureClass { Archer, Tank, Mage }` + `percentage`
    (default 0.2). +percentage damage **and** HP for *every* player creature of that class (matches
    by `ArcherSO`/`TankSO`/`MageSO` subtype, so Big variants and any future creature of the class
    are covered). Assets: `boost_archer`/`boost_tank`/`boost_mage`.
  - **`ActionStatBoostSO`** — `action` + `percentage`. Scales one nuke/spell's main number:
    Fire Magic damage (`boost_firemagic`, 0.2), Shield HP and heal (`boost_shield`, 0.2), Battle
    Cry's **bonus part only** (`boost_battlecry`, 0.6 — ×1.24 → `1 + 0.24 × 1.6` = ×1.384).
  - **`ActionImprovementSO`** (abstract) — `action` (icon fallback only). Changes a mechanic
    instead of a number; each concrete subclass is the key its resolver looks up and carries its own
    tunables:
    - **`ShockImprovementSO`** (`boost_shock`) — `shieldDamagePerBolt` (10). Shock bolts damage a
      standing enemy shield instead of being blocked; leftover bolts after it breaks carry on to the
      normal priority targets (`docs/ActionsAndSpells.md` 2c).
    - **`StarfallImprovementSO`** (`boost_starfall`) — `extraStrikes` (1). After the main volley,
      extra stars hit random enemies that survive it (`StarfallAnimation.extraStrikeDelay`, 0.5s).
    - **`CharmImprovementSO`** (`boost_charm`) — `shockOnFailChance` (0.5). A failed Charm may shock
      its target.

## `RewardListSO`

`_Global/_Campaign/RewardListSO.cs` — top-level catalog, same shape as `GameCatalog`:
`List<RewardSO> allRewards` + `Find(string id)`. Asset:
`_ScriptableObjects/_Campaign/RewardListSO.asset`. Exposed via the static `G.RewardList` accessor,
but — like `EncounterListSO`/`G.EncounterList` — the field itself is **not** serialized on `G`
(which is per-scene and doesn't exist in `MapScene`). It lives on `CampaignStateManager`
instead: `G.RewardList => CampaignStateManager.Instance.RewardList`. `CampaignStateManager`
lives on `_Prefabs/_Campaign/CampaignProgress.prefab` (see `docs/Campaign.md`), instanced in
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

A fight's guaranteed reward (`RewardEncounter.Play(int rewardAmount)`, sourced from
`FightSO.rewardAmount`) and `BonusEnergyRewardSO.Claim()` both do a plain
`run.currentEnergy += reward` — **not** `Mathf.Min(current + reward,
energyCapacity)`. This used to clamp (a pre-existing behavior on the original single-reward
`RewardPickSO` — since folded into `FightSO`, see `docs/Encounters.md` — inherited by
`BonusEnergyRewardSO` when it was added), which was a real, live-
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

## `RewardBonuses` — the one boost reader (player side only)

`_Global/_Campaign/RewardBonuses.cs` is the only code that reads `RunState.boostRewardIds`. Every
query takes `bool isPlayer` and returns the base value / `false` for the enemy — **rewards never
apply to the enemy**, even though both sides roll the same nuke/spell assets (`G.enemyNukes`/
`enemySpells` point at the player's defaults in `BattleScene`):

```csharp
public static float ApplyActionBonus(ActionSO action, float baseValue, bool isPlayer);   // ActionStatBoostSO
public static float ActionBonusFraction(ActionSO action, bool isPlayer);                 // summed % (Battle Cry)
public static float ApplyCreatureBonus(CreatureSO data, float baseValue, bool isPlayer); // CreatureClassBoostSO
public static bool TryGetImprovement<T>(bool isPlayer, out T improvement) where T : ActionImprovementSO;
```

Resolvers don't call it directly — `ActionResolver` (the Nuke/Spell resolver base) wraps it, with
the side derived from the caster (`caster == G.PlayerHero`):

```csharp
protected static bool IsPlayer(Hero caster);
protected static bool IsImproved<T>(Hero caster, out T improvement) where T : ActionImprovementSO;
protected static float Boosted(ActionSO source, Hero caster, float baseValue);

// usage, e.g. ShockResolver:
if (IsImproved(caster, out ShockImprovementSO improvement)) { /* altered logic */ }
```

**Adding a new improvement:** subclass `ActionImprovementSO` (tunables + `[Tooltip]`s on it),
create the asset in `_ScriptableObjects/_Campaign/_Rewards/`, add it to `RewardListSO.asset`, and
branch on `IsImproved(caster, out YourImprovementSO imp)` inside that action's resolver. All data
mutation still goes through shots (rule 7) — any RNG is rolled in the resolver.

Call sites (all bonus reads live at the resolver/combat-resolution level, never in the SOs — rule 13):

- **Creature damage** — `AttacksResolver.Mechanics.cs`'s `ResolveTeam(..., isPlayerSide)` wraps
  `stats.damage` with `ApplyCreatureBonus` (before BattleCry/modifiers/crit); `EstimateFirepower`
  mirrors it so AI estimates match.
- **Creature HP at spawn** — `Creature.GetMaxHealth()`, `ApplyCreatureBonus(Data, health,
  OwnerHero == G.PlayerHero)`. A creature the player charms from the enemy gets the damage bonus
  while on the player's side (side is read per attack) but keeps its spawn-time HP.
- **Fire Magic damage** — `FireMagicResolver`, `Boosted(...)`.
- **Battle Cry buff** — `BattleCryResolver.BoostedBonusPart`: `1 + (m − 1) × (1 + fraction)`. The
  debuff is never boosted.
- **Shield HP / heal** — `ShieldResolver` computes `MaxHp` (spawn/promote shots → `Shield.Init/
  Promote(level, maxHp)`) and `Heal` with `Boosted(...)`. `Shield` itself no longer reads bonuses.
- **Improvements** — `ShockResolver`, `StarfallResolver`, `CharmResolver` via `IsImproved<T>`
  (see `docs/ActionsAndSpells.md`).

Old saves that claimed the pre-rework ids (`boost_bubka`/`boost_golem`/`boost_dragon`) just resolve
to `null` in `RewardList.Find` and are ignored.

## Card-draw algorithm — `RewardDrawer.DrawThree`

Pools, after excluding every `RewardSO` where `unique && IsOwned(run)`:

- **hpOrEnergy** = `HpBoostRewardSO` ∪ `BonusEnergyRewardSO` (never filtered — not unique)
- **creature** = unowned `CreatureRewardSO`
- **boost** = unowned `BoostSO` (all subclasses)

Each slot tries its pools in order, skipping any card already shown in this draw (a `used` set —
applies to non-unique cards too, so Vitality and Energy Cell never appear twice while the other
one is available):

- Slot 1: **hpOrEnergy**
- Slot 2: **creature → hpOrEnergy** (i.e. once every creature is claimed, slot 2 shows whichever of
  Vitality/Energy Cell slot 1 didn't)
- Slot 3: **boost → creature → hpOrEnergy**

If every pool is exhausted for a slot, it repeats a random card from its last pool (hpOrEnergy —
always valid, never unique). Resulting draws:

- Default: `[hpOrEnergy, creature, boost]`
- All creatures claimed: `[Vitality|Energy, the other one, boost]`
- Creatures and boosts all claimed: `[Vitality|Energy, the other one, repeat]`

## `RewardCard` / `RewardEncounter` flow

**`RewardEncounter` splits into a backend and a view (CLAUDE.md rule 28)**, so the whole pick can be
driven headlessly by test code with zero UI:

```csharp
public class RewardEncounter : Encounter
{
    public int RewardAmount { get; private set; }
    public IReadOnlyList<RewardSO> DrawnRewards { get; private set; }
    public RewardSO SelectedReward { get; private set; }

    public event Action<IReadOnlyList<RewardSO>> OnRewardsDrawn;
    public event Action<RewardSO> OnSelectionChanged;
    public event Action OnClaimed;

    public void Play(int rewardAmount)
    {
        RewardAmount = rewardAmount;
        var run = CampaignStateManager.Instance.CurrentRun;
        run.currentEnergy += RewardAmount;
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

`RewardEncounter` has **no UI reference of any kind** — `RewardAmount`/`DrawnRewards`/`SelectedReward`
are the entire "menu" a test controller needs (`RewardSO` objects, not `RewardCard` visuals), and
`SelectReward`/`Confirm` are the entire "input" surface. `Confirm()`'s `RunState` mutation and
`Save()` always run unconditionally; only whether `Complete()` fires immediately (`Headless`) or
waits for the paired view depends on which mode it's running in — see CLAUDE.md rule 28.

`RewardCard` (`_Global/_Campaign/_Rewards/RewardCard.cs`, prefab
`_Prefabs/_UI/_Cards/RewardCard.prefab`) — icon/name/type/description display, a single full-card
`Button` as the click target (clicking anywhere on the card selects it — there's no separate
sub-button), `Init(RewardSO)` (reads `EffectiveIcon`), `SetSelected(bool)` (delegates to the
sibling `RewardCardAnimator`'s `PlaySelect()`/`PlayDeselect()`), `event Action<RewardCard>
OnClicked`. No Claim button here — Claim lives once on `RewardEncounterView`, shared across all 3
spawned cards.

`RewardCardAnimator` (`_Global/_Campaign/_Rewards/RewardCardAnimator.cs`, `[RequireComponent]`d by
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

**`RewardEncounter` (backend) `Play(int rewardAmount)`:**

1. Grants the guaranteed energy immediately (`run.currentEnergy += rewardAmount`, uncapped — see
   "Energy grants are uncapped" below), saves.
2. Draws via `DrawRewards()` (`DebugRewards.ConsumeOverrideDraw() ?? RewardDrawer.DrawThree(...)`),
   sets `DrawnRewards`, fires `OnRewardsDrawn`.

**`RewardEncounterView` reacts to that event:** sets `messageText.text = "You got {N} energy!"`
(reading `_backend.RewardAmount`),
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

`_Global/_Campaign/DebugRewards.cs` — a component on `CampaignProgress.prefab` alongside
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
- **Icons:** action/creature art lives in `Assets/Game/_Sprites/_Slot_Cards/_Creatures|_Nukes|_Spells/`
  (each `ActionSO.cardSprite`), reward-only art in `Assets/Game/_Sprites/_Rewards/`.
  `hpboost_15`/`bonusenergy_50` use `health.png`/`energy.png` (plus already drawn in), the three
  `CreatureClassBoostSO`s use `archer.png`/`tank.png`/`mage.png` as explicit `icon`s (no single
  action to fall back to), and `ActionStatBoostSO`/`ActionImprovementSO` fall back to their action's
  `cardSprite`. The Big creature unlocks still use placeholder art.
- **Boost overlay:** `RewardCard.prefab` has a hidden `BoostOverlay` child ("++"), wired to
  `RewardCard.boostOverlay`. `RewardCard.Init(RewardSO)` sets it active from
  `RewardSO.ShowsBoostOverlay` — `true` for every `BoostSO` (class boosts, stat boosts,
  improvements), `false` otherwise (Vitality/Energy Cell icons carry their own plus; creature
  unlocks aren't upgrades). The loadout Comparison overload `Init(ActionSO, ...)` always hides it.
- **Auditing:** the `validate-rewards` skill (`.claude/skills/validate-rewards/SKILL.md`) is the
  checklist for a full functional/text/icon/reachability/balance audit of this system.
- **Changing a reward asset's script type** (e.g. a boost moving to a new `BoostSO` subclass) by
  editing its `m_Script` guid in YAML leaves an already-loaded `RewardListSO` holding a stale
  `null` in the Editor until a domain reload / `Resources.UnloadAsset` — the serialized reference
  is fine, only the in-memory list is stale.
- **All 15 reward assets live in `_ScriptableObjects/_Campaign/_Rewards/`**, referenced by
  `RewardListSO.asset`. The 4 reward creatures (`BubkaBig`, id `archer_big`, 1.5x prefab scale, plus
  `Lizard`, id `lizard`, `Bulba`, id `bulba`, and `HealingShroom`, id `healing_shroom`) back the 4
  `CreatureRewardSO`s. They're registered in
  `GameCatalog.asset`'s `allCreatures` alongside the originals. The `setup-creature` skill's
  "Player reward creature" branch is the checklist for adding or replacing one.
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

- `docs/Encounters.md` — `FightSO.hasReward`/`rewardAmount`/`RewardEncounter`'s original
  single-guaranteed-reward shape this system extends, and the `Encounter`/`BattleRewardPresenter`
  dispatch that instantiates it.
- `docs/Campaign.md` — `RunState`, `CampaignProfileSO`, `CampaignDebugTool`, and the `ActionSO.id`/
  `GameCatalog` id-resolution pattern `RewardSO.id`/`RewardListSO` mirrors.
- `docs/ActionsAndSpells.md`, `docs/Battle.md` — the resolvers `RewardBonuses` is read from, and
  the improved Shock/Starfall/Charm behavior.
