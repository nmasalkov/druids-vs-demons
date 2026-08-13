using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One small icon-only card: used for the 9 fixed equipped-loadout-slot anchors and the dynamic
/// Available pool grid in LoadoutPickEncounterView. Pure display + a single click target — no
/// selection animation (BackGroundSelected is a plain background swap, not a transform tween, so
/// CLAUDE.md rule 27 doesn't apply here). The root GameObject has no Graphic of its own, so the
/// click target is BackGroundUnselected's Button (see docs/Loadout.md's Editor setup checklist).
/// </summary>
public class MiniCard : MonoBehaviour
{
    private static readonly Color GreyedOutColor = new Color32(0x6A, 0x50, 0x50, 0xFF);

    [SerializeField] private Image cardImage;
    [SerializeField] private GameObject backgroundSelected;
    [SerializeField] private Button button;

    public ActionSO Data { get; private set; }
    public event Action<MiniCard> OnClicked;

    void Start() => button.onClick.AddListener(NotifyClicked);
    void OnDestroy() => button.onClick.RemoveListener(NotifyClicked);

    public void Init(ActionSO action)
    {
        Data = action;
        cardImage.sprite = action.cardSprite;
        cardImage.color = Color.white;
    }

    public void SetSelected(bool selected) => backgroundSelected.SetActive(selected);

    /// <summary>Tints CardImage instead of toggling Button.interactable — a plain color tint avoids
    /// Button's own Disabled Color transition (semi-transparent by default), and greyed-out pool
    /// cards still need to route clicks to LoadoutPickEncounter.SelectCandidate so it can no-op them
    /// via IsSelectable rather than the click never registering at all.</summary>
    public void SetGreyedOut(bool greyedOut) => cardImage.color = greyedOut ? GreyedOutColor : Color.white;

    private void NotifyClicked() => OnClicked?.Invoke(this);
}
