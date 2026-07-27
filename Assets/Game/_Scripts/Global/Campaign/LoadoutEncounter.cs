using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder pre-fight encounter: shows LoadoutPickSO.briefingText, dismissed by a click anywhere
/// on the panel. Uses a real UGUI Button (routed through Unity's EventSystem/raycaster) rather than
/// polling Mouse.current directly — raw device polling isn't scoped to the game view, so a click
/// anywhere in the Editor (Inspector, Hierarchy, ...) would silently dismiss this while Play mode is
/// merely running in the background. See docs/Encounters.md.
/// </summary>
public class LoadoutEncounter : Encounter
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button panelButton;

    void Start() => panelButton.onClick.AddListener(Complete);
    void OnDestroy() => panelButton.onClick.RemoveListener(Complete);

    public override void Play(EncounterSO data)
    {
        messageText.text = ((LoadoutPickSO)data).briefingText;
    }
}
