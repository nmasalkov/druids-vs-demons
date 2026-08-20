# Loadout Picker

## What this system does

The pre-battle loadout picker lets the player review and swap their 9 equipped loadout slots (tank,
archer, mage, 3 nukes, 3 spells — the same `RunState` id fields `docs/Campaign.md` documents) against
whatever else they've gathered, before advancing to the next encounter. `LoadoutPickEncounter` (the
backend) and `LoadoutPickEncounterView` (the UI) follow the same headless-testable backend/view split
as `RewardEncounter`/`RewardEncounterView` (CLAUDE.md rule 28, `docs/Rewards.md`) — the backend never
touches a UI reference, and only `Confirm()` ever persists into `RunState`. Played by `MapManager`, in
place at the current map node, whenever the current fight's `FightSO.hasLoadoutPick` is true — see
`docs/Encounters.md`. Same `_Prefabs/Campaign/LoadoutEncounter.prefab` that used to host a trivial
briefing-text placeholder.

## `SlotKind`/`SlotRef` addressing

`Global/Campaign/LoadoutPickEncounter.cs` nests both (CLAUDE.md rule 12 — single-owner, promoted to
public since the paired View needs them):

```csharp
public enum SlotKind { Tank, Archer, Mage, Nuke, Spell }

public readonly struct SlotRef : IEquatable<SlotRef>
{
    public readonly SlotKind Kind;
    public readonly int Index; // meaningful only for Nuke/Spell (0-2); 0 for Tank/Archer/Mage
}
```

A single `SlotRef` addresses any of the 9 physical slots, replacing what used to be five near-
duplicate `GetArcherOptions()`/`GetTankOptions()`/`GetMageOptions()`/`GetNukeOptions()`/
`GetSpellOptions()` methods plus a separate `Slot{A,B,C}` enum. Nuke/spell candidate filtering is
**category-only** — `NukeSO`/`SpellSO` have no subtype distinguishing them by creature role the way
`ArcherSO`/`TankSO`/`MageSO` do, so any nuke fits any of the 3 nuke slots (same for spells); a
`SlotRef(SlotKind.Nuke, 1)` candidate list is just "every unequipped nuke."

## `LoadoutPickEncounter` (backend)

```csharp
public class LoadoutPickEncounter : Encounter
{
    public readonly struct State
    {
        public readonly IReadOnlyDictionary<SlotRef, ActionSO> Equipped;
        public readonly IReadOnlyList<ActionSO> AvailablePool;
        public readonly SlotRef? SelectedSlot;
        public readonly ActionSO SelectedCandidate;

        public bool IsSlotSelectable(SlotRef slot) { /* SelectedCandidate == null || CategoryMatches(slot.Kind, SelectedCandidate) */ }
        public bool IsCandidateSelectable(ActionSO candidate) { /* !SelectedSlot.HasValue || CategoryMatches(SelectedSlot.Value.Kind, candidate) */ }
    }

    public SlotRef? SelectedSlot { get; private set; }
    public ActionSO SelectedCandidate { get; private set; }

    public event Action OnLoadoutLoaded;
    public event Action OnPoolChanged;
    public event Action OnSelectionChanged;
    public event Action OnConfirmed;

    public void Play() { /* seeds all 9 pending fields from RunState/GameCatalog, RefreshPool(), fires OnPoolChanged + OnLoadoutLoaded */ }
    public State GetState() { /* one snapshot: BuildEquippedLookup(), the pool, SelectedSlot, SelectedCandidate */ }
    public bool IsSlotSelectable(SlotRef slot) { /* live-field version of State's own predicate — used by SetSelectedSlot's guard */ }
    public bool IsCandidateSelectable(ActionSO candidate) { /* live-field version — used by SetSelectedCandidate's guard */ }
    public void SetSelectedSlot(SlotRef slot) { /* no-ops unless IsSlotSelectable(slot); re-clicking the current slot untargets */ }
    public void SetSelectedCandidate(ActionSO candidate) { /* no-ops unless IsCandidateSelectable(candidate); re-clicking the current candidate untargets */ }
    public void Swap() { /* writes SelectedCandidate into SelectedSlot's pending field, clears selection, RefreshPool() */ }
    public void Confirm() { /* writes all 9 pending fields into RunState, Save(), OnConfirmed, if(Headless) CompletePresentation() */ }
}
```

### Bidirectional selection

Either half of the selection can be picked first — an equipped slot (`SetSelectedSlot`) or a pool
candidate (`SetSelectedCandidate`) — and the other side reacts by greying out everything that
wouldn't match. This works off two symmetric guard predicates, each null-safe on the *other* half:

```csharp
IsSlotSelectable(slot)      => SelectedCandidate == null || CategoryMatches(slot.Kind, SelectedCandidate);
IsCandidateSelectable(cand) => !SelectedSlot.HasValue || CategoryMatches(SelectedSlot.Value.Kind, cand);
```

Both setters check their own predicate before doing anything, so **`SelectedSlot` and
`SelectedCandidate` can never disagree in category** — no manual reconciliation ("clear the candidate
if it no longer matches") is needed anywhere, and `Swap()`'s cast in `SetPending` can never throw from
a mismatch. Concretely:

- Nothing selected → either side can be clicked first and becomes the anchor for the other side's
  graying.
- Anchor side (whichever type has a selection) is always freely clickable among its own items —
  re-clicking the current selection untargets it; clicking a different matching item re-targets.
- Reactive side (the type *without* a selection) only accepts items matching the anchor's category —
  a click on a greyed item is a no-op, exactly like today's pool-graying already worked.
- Once **both** are set, clicking a non-matching item on *either* side is a no-op too — e.g. with a
  Tank slot + a Tank candidate both selected, clicking a Nuke slot is blocked instead of silently
  abandoning the candidate. This is the one behavior change from the original slot-first-only design:
  equipped-slot graying is now meaningful (matches what "greyed = can't be selected" already means for
  the pool side), not just a decoration that only ever applied to the pool.

The View drives graying for **both** groups off the exact same predicates via `State`: equipped cards
use `!state.IsSlotSelectable(slot)`, pool cards use `!state.IsCandidateSelectable(card.Data)`.

### `GetState()` — one snapshot instead of piecemeal reads

`State` (nested per rule 12, promoted public since the View needs it) bundles everything a redraw
needs — the 9 equipped slots' contents, the Available pool, and both selection halves — into one
`GetState()` call, replacing what used to be three separate reads (`CurrentPool`, `GetEquipped(slot)`
called once per slot, `SelectedSlot`/`SelectedCandidate` read directly) spread across multiple event
handlers (CLAUDE.md rule 28's state-snapshot addendum). It's a **transient read**, not a cache — the
View never holds a `State` across frames, it re-fetches a fresh one on every redraw; the source of
truth stays on the backend's own private fields, exactly as rule 22 requires.

- **`Confirm()` is unaffected by `SelectedSlot`/`SelectedCandidate`** — only `Swap()` ever writes the
  pending backing fields `Confirm()` reads, exactly like `RewardEncounter`'s `SelectReward()`/
  `Confirm()` split: selection alone never mutates `RunState`.
- **Fully headless-drivable** — every method above is pure data/logic, no UI dependency anywhere:
  ```csharp
  Headless = true;
  Play();
  SetSelectedCandidate(candidate);              // pool-first this time
  SetSelectedSlot(new SlotRef(SlotKind.Nuke, 1));
  Swap();
  Confirm(); // RunState.nukeBId now == candidate.id, OnCompleted fires
  ```

## `MiniCard`

`Global/Campaign/MiniCard.cs` — a small icon-only display card used both for the 9 fixed equipped-
slot anchors and the dynamic Available pool grid. `Init(ActionSO)` sets `CardImage`'s sprite;
`SetSelected(bool)` toggles the `BackGroundSelected` child GameObject active/inactive — a plain
background swap, not a transform tween, so CLAUDE.md rule 27 (DOTween via a dedicated Animator)
doesn't apply here. `SetGreyedOut(bool)` tints `CardImage`'s color (`0x6A5050` when greyed, white
otherwise) instead of touching `Button.interactable` — see the `RewardCard` overload below for why
`interactable` is avoided for a non-standard visual state.

**The `Button` lives on the `MiniCard` prefab root, not on a child.** `CardImage` and
`BackGroundUnselected` are siblings, both with `Image.raycastTarget = true`; whichever one the pointer
actually lands on is what `GraphicRaycaster` hits. Unity resolves the click *handler* by walking
**up** from that hit object through its ancestors (`ExecuteEvents.GetEventHandler`) — it never crosses
to a sibling. A `Button` sitting on `BackGroundUnselected` is therefore invisible to a click that lands
on `CardImage` (which covers most of the card): the walk-up from `CardImage` passes through the
`MiniCard` root and never touches its sibling `BackGroundUnselected`, so most of the card is dead
space and only a card's exposed edge pixels register a click. This was a real, live-caught bug — real
clicks landed on "a random one or two" cards depending on how much of `BackGroundUnselected`'s border
happened to be left exposed around each card's icon. Fixing it means putting `Button` on an *ancestor*
of every raycastable graphic in the card, i.e. the `MiniCard` root itself (`targetGraphic` still points
at `BackGroundUnselected`'s `Image` for the color-tint feedback) — now a hit on either `CardImage` or
`BackGroundUnselected` walks up to the same root and finds the `Button` every time.

**The `MiniCard` prefab root's anchors must be `(0.5, 0.5)` (center), not `(0, 0)` (parent's
bottom-left corner).** This was a second real, live-caught bug: the source prefab's default anchors
were `(0, 0)`, so a fresh `Instantiate()` under any equipped-slot anchor (which has no `LayoutGroup` of
its own) placed the card's pivot at the parent's corner instead of its center, visibly shifting every
equipped-slot card away from where its placeholder appeared in the Editor. The hand-placed placeholder
instances in `LoadoutEncounter.prefab` had `(0.5, 0.5)` as a **per-instance override**, which is why
they looked correct in the Editor (rule 24's disabled-placeholder pattern) while every runtime spawn
from the *source* prefab was broken — exactly the same class of bug as the `RewardCard` scale issue
below. The Available pool's `GridLayoutGroup`-managed cards were unaffected either way (the layout
group overrides each child's anchor/position outright), so this bug only showed up in "Your Deck".

## `RewardCard.Init(ActionSO, string)` overload

The `Comparison` panel's `Left Card`/`Right Card` reuse the existing `RewardCard` component
(`docs/Rewards.md`) via a second, additive `Init` overload rather than a separate display component —
the prefab already had `RewardCard` instances placed in both anchors:

```csharp
public void Init(ActionSO action, string typeLabel)
{
    iconImage.sprite = action.cardSprite;
    nameText.text = action.actionName;
    typeText.text = typeLabel;
    descriptionText.text = action.description;
}
```

`description` (`[TextArea] public string description;`, matching `RewardSO`'s own field) lives on
`ActionSO` itself, so every subtype (`CreatureSO`/`TankSO`/`ArcherSO`/`MageSO`, `NukeSO`, `SpellSO`)
gets it for free — populated with short flavor text on all 13 real asset instances under
`Assets/Game/_ScriptableObjects/Actions/**`.

`Data` (`RewardSO`-typed) stays `null` on cards populated this way — nothing in
`LoadoutPickEncounterView` reads it back, only `Init`'s own parameters matter. Deliberately does
**not** set `button.interactable = false`, even though these cards are display-only and
`LoadoutPickEncounterView` never wires their `OnClicked` — a real, live-caught bug: `Button`'s
`Transition = ColorTint` repaints `targetGraphic` to `m_DisabledColor` (Unity's default is a
semi-transparent grey) the moment `interactable` goes `false`, which is exactly the "somehow
semi-transparent" look the Comparison cards had. Since nothing ever reads `interactable` for these
instances anyway, the fix is simply to leave it alone.

## `LoadoutPickEncounterView` (view)

`[RequireComponent(typeof(LoadoutPickEncounter))]`, subscribes to the backend's events in `Awake()`
(not `Start()`) — same reasoning as `RewardEncounterView`: `MapManager` calls
`Instantiate()` then `Play()` synchronously in the same method, and `Play()` fires `OnLoadoutLoaded`/
`OnPoolChanged` inline, so a `Start()`-based subscription would miss them.

**One `Redraw()`, fed by `GetState()`, instead of a handler per event doing its own narrow backend
query.** Only two things are genuinely structural (`Instantiate`/`Destroy`, not just re-`Init` on an
existing instance) and get their own listener — everything else funnels through the shared `Redraw()`:

- `OnLoadoutLoaded` → `SpawnLoadoutCards` — spawns all 9 equipped-slot `MiniCard`s once (via an
  anchor-lookup `Dictionary<SlotRef, Transform>` built in `Awake()`, replacing each disabled prefab
  placeholder per CLAUDE.md rule 24), locates the 2 reusable Comparison `RewardCard` placeholders via
  `GetComponentInChildren<RewardCard>(true)` (no `Instantiate` at all — the placeholders carry
  hand-tuned instance overrides, `localScale ≈ 0.558`/`sizeDelta ≈ 251×356`, that make them fit the
  Comparison panel; instantiating fresh from the prefab asset loses those overrides and is why the
  Comparison cards used to render giant), then calls `Redraw()`.
- `OnPoolChanged` → `RebuildAvailablePool` — the one genuinely variable-length list (rule 24's own
  carve-out): destroys every child of `ActionsContainer` and rebuilds from `state.AvailablePool`, one
  `MiniCard` per action forwarding its click to `SelectPoolCandidate`. Fires only on load and after a
  real `Swap()`, never from mere browsing. **Must not call the shared `Redraw()`** — on its very first
  firing (from `Play()`, which fires `OnPoolChanged` *before* `OnLoadoutLoaded`), the 9 equipped
  `MiniCard`s don't exist yet, so `DrawEquippedSlots` would throw; it only draws its own region
  (`DrawAvailablePool`). The `OnLoadoutLoaded`/`OnSelectionChanged` firings that follow both call the
  full `Redraw()` and cover the rest.
- `OnSelectionChanged` → `Redraw` directly (no separate handler method) — pulls one `GetState()`
  snapshot and hands it to three region-specific draw functions:
  - `DrawEquippedSlots(state)` — re-`Init`s all 9 equipped cards from `state.Equipped[slot]` (cheap;
    redrawing all 9 unconditionally on every change, including after a `Swap()`, is simpler than
    tracking which single slot changed — the old dedicated `OnSwapped(SlotRef)` event was removed for
    exactly this reason), `SetSelected` against `state.SelectedSlot`, `SetGreyedOut` from
    `!state.IsSlotSelectable(slot)`.
  - `DrawAvailablePool(state)` — `SetSelected`/`SetGreyedOut` (`!state.IsCandidateSelectable(...)`) on
    the *existing* pool cards — no rebuild; also called directly (not via `Redraw()`) from
    `RebuildAvailablePool` right after a structural rebuild, so freshly-spawned cards start correctly
    drawn too.
  - `DrawComparisonPanel(state)` → `DrawLeftCardForSwap(state)`/`DrawRightCardForSwap(state)`: Left and
    Right are fully **independent** of each other — each shows as soon as its own half of the
    selection exists, regardless of order or whether the other half is set yet. Picking an equipped
    slot first shows Left immediately (forward flow's first click); picking a pool card first shows
    Right immediately (reverse flow's first click) — the exact same milestone, mirrored, not gated on
    the other half. Swap only ever enables once both are set. The type label (`GetTypeLabel(ActionSO)`)
    is derived from the action instance itself, not from `SelectedSlot.Kind` — Right needs this even
    when no slot has been picked yet. Verified live via a real simulated click on a pool card with
    nothing else selected: Right correctly appeared immediately, Left correctly stayed hidden (no slot
    known yet to show "what's currently there").
  - Both `_leftCard`/`_rightCard` are already clickable (`RewardCard`'s own full-card `Button`, same
    as a reward pick) — wired to **clear their own half of the selection**: clicking Left calls
    `SetSelectedSlot(SelectedSlot.Value)` (re-invoking with its own current value, which
    `SetSelectedSlot`'s existing re-click-to-deselect toggle turns into a clear — no new backend
    method needed), clicking Right does the same via `SetSelectedCandidate(SelectedCandidate)`. Safe
    without a null-check on either (rule 5) because each card is only ever active while its own half
    is actually set.
- `OnConfirmed` → `ClosePresentation` — calls `_backend.CompletePresentation()` immediately (no exit
  animation — revisit if a transition beat is wanted later, mirroring
  `RewardEncounterView.HandleClaimed()`'s discard-delay shape).

Click → backend method mapping: an equipped-slot `MiniCard` click → `SetSelectedSlot(slot)`; an
Available-pool `MiniCard` click → `SelectPoolCandidate(card) => SetSelectedCandidate(card.Data)` (a
no-op if the card is currently greyed out, since `SetSelectedCandidate` itself checks
`IsCandidateSelectable`); `Swap Button` → `RequestSwap` → `Swap()`; `Finish` button →
`ConfirmLoadout` → `Confirm()`.

Naming convention (CLAUDE.md rule 28's addendum, established here): every event listener and internal
button-click handler is named for what it actually does (`RebuildAvailablePool`, `ClosePresentation`,
`RequestSwap`, `NotifyClicked` on `MiniCard`/`RewardCard`'s own `button.onClick`), never
`Handle<EventName>` — the method name should tell a reader something the event's own name doesn't.

## Editor setup checklist

- **`Assets/Game/_Prefabs/UI/MiniCards/MiniCard.prefab`** (source prefab — propagates to every
  instance placed in `LoadoutEncounter.prefab` automatically): root RectTransform anchors
  `(0.5, 0.5)` (center — see the `MiniCard` section above for why this matters); `Button` component
  on the **root** GameObject (not `BackGroundUnselected` — see the `MiniCard` section above for why),
  `targetGraphic` → `BackGroundUnselected`'s `Image`; `MiniCard` component also on the root,
  `cardImage` → `CardImage`'s `Image`, `backgroundSelected` → `BackGroundSelected`, `button` → the
  root's own `Button`.
- **`Assets/Game/_Prefabs/Campaign/LoadoutEncounter.prefab`** root: `LoadoutPickEncounter` +
  `LoadoutPickEncounterView` components (replacing the old placeholder `LoadoutEncounter` component).
  `OverlayPanel`'s old click-anywhere-dismiss `Button` was removed (the `Finish` button drives
  completion instead); its `Image` stays as the background. `LoadoutPickEncounterView`'s fields wire
  to the prefab's existing named anchors: `tankSlotAnchor`/`archerSlotAnchor`/`mageSlotAnchor` →
  `TankSlot`/`Archer Slot`/`MageSlot`; `nukeSlotAnchors[0..2]` → `Nuke Slot 1/2/3`;
  `spellSlotAnchors[0..2]` → `Spell Slot 1/2/3`; `availableContainer` → `Available/ActionsContainer`;
  `leftCardAnchor`/`rightCardAnchor` → `Comparison/Left Card`/`Right Card`; `swapButton` →
  `Comparison/Swap Button`; `finishButton` → `Finish`; `miniCardPrefab` → `MiniCard.prefab`. There is
  no `comparisonCardPrefab` field — the Comparison cards reuse whatever `RewardCard` instance is
  already parented under `leftCardAnchor`/`rightCardAnchor` (see `LoadoutPickEncounterView` above), so
  those two placeholders must stay in place. `Swap Button`'s `interactable` starts unchecked in the
  prefab (defense-in-depth alongside the code-enforced default).
- All existing disabled `CardArcher` (`MiniCard` instance) and `RewardCard` placeholder children stay
  in place under their anchors — code replaces (`MiniCard`) or directly reuses (`RewardCard`) them
  starting from the first `Play()`.

## Testing UI clicks live via Coplay MCP

None of Coplay's dedicated tools simulate a pointer click — `play_game` + `capture_ui_canvas` only get
you a screenshot, not interaction. To actually verify a UI element is clickable (not just visible),
fall back to `execute_script` with a raycast-based click simulation — this is what actually caught the
`MiniCard`/`RewardCard` bugs documented above, screenshots alone would not have (a screenshot showing
cards in the right place doesn't tell you whether clicking them does anything).

**The recipe** — this raycasts from a real screen position through `EventSystem`/`GraphicRaycaster`
exactly like a real click would, so it exercises the *same* hit-testing and handler-resolution path a
player's mouse does (catches "wrong object receives the click" bugs a direct
`button.onClick.Invoke()` call would silently paper over):

```csharp
// 1. Compute a screen position over the actual visual element you want to click (not
//    necessarily the object holding the Button — click on a decorative child on purpose to
//    catch raycast-routing bugs like the one this doc describes above).
var targetTransform = someMiniCard.transform.Find("CardImage");
Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, targetTransform.position); // null camera: Screen Space - Overlay canvas

// 2. Raycast through the real UI event pipeline, exactly like a mouse click would.
var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
var results = new List<RaycastResult>();
EventSystem.current.RaycastAll(pointerData, results);
Debug.Log($"Raycast hits: {string.Join(", ", results.Select(r => r.gameObject.name))}"); // sanity check — is the right object even reachable?

// 3. Resolve the click handler the same way Unity's EventSystem does (walks UP from the hit
//    object through its ancestors) and fire it.
var handler = results.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject) : null;
Debug.Log($"Click handler resolved to: {(handler != null ? handler.name : "NONE")}"); // NONE here is exactly the bug this doc's MiniCard section describes
if (handler != null) ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerClickHandler);

// 4. Assert against the backend's public state, not the UI — the backend is the source of truth.
Debug.Log($"SelectedSlot after click: {backend.SelectedSlot}");
```

Run via `execute_script` (needs `using UnityEngine.EventSystems;`, `System.Collections.Generic`,
`System.Linq`), then read results back with `get_unity_logs` (`search_term` scoped to a unique log
prefix, e.g. `[SimClick]`, keeps the output readable). Follow with `capture_ui_canvas` to visually
confirm the resulting state (selection highlight, greyed cards, Comparison panel contents/size).

**Gotchas specific to this recipe:**
- `RectTransformUtility.WorldToScreenPoint`'s camera argument must be `null` for a Screen Space -
  Overlay canvas (`Canvas.renderMode == 0`) — passing the scene camera silently produces a wrong
  screen position and the raycast misses everything.
- Log the raycast hit list (step 2) even when you expect it to work — an empty or unexpected hit list
  immediately tells you whether the problem is visibility/layout (nothing hit, or the wrong object was
  hit) versus handler resolution (something was hit, but `GetEventHandler` returned `NONE`), which are
  different bugs with different fixes.
- Play-mode state persists across separate `execute_script` calls as long as the session stays in Play
  mode (`stop_game` not yet called) — so a multi-step interaction (select a slot, then separately
  select a candidate, then Swap) can be split across several `execute_script` calls instead of one
  giant script, which also sidesteps the frame-deferred `Start()`/`Destroy()` gotchas already
  documented in `CLAUDE.md`'s Coplay MCP section (each tool call is a genuinely later frame, not a
  same-frame continuation).

## Related docs

- `docs/Encounters.md` — `Encounter`/`MapManager` dispatch this plays through, `FightSO.hasLoadoutPick`.
- `docs/Rewards.md` — the sibling pick-screen system this mirrors: `RewardEncounter`/
  `RewardEncounterView`, `RewardCard`/`RewardCardAnimator`.
- `docs/Campaign.md` — `RunState`'s 9 loadout id fields and `gatheredCreatureIds`/`gatheredNukeIds`/
  `gatheredSpellIds`, `GameCatalog`.
