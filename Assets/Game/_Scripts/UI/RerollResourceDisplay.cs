using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shows a reroll resource count (player energy, enemy reroll pool, ...) and plays a feedback only
/// when it's actually spent. Text updates on every value change; each subclass wires the "changed"
/// event to UpdateText and the (separate) "spent" event to PlayChangeFeedback, mirroring
/// EnergyController's OnEnergyChanged/OnEnergySpent split — see docs/Energy.md's "Two energy
/// events" section for why the feedback must not also play on a plain value reset/reapply.
/// </summary>
public abstract class RerollResourceDisplay : MonoBehaviour
{
    [FormerlySerializedAs("energyText")] [SerializeField] private TMP_Text quantityText;
    [FormerlySerializedAs("energyChangeFeedback")] [SerializeField] private MMF_Player changeFeedback;

    protected abstract int CurrentValue { get; }

    void Start()
    {
        UpdateText(CurrentValue);
        Subscribe();
    }

    void OnDestroy() => Unsubscribe();

    protected abstract void Subscribe();
    protected abstract void Unsubscribe();

    protected void UpdateText(int value) => quantityText.text = value.ToString();
    protected void PlayChangeFeedback() => changeFeedback.PlayFeedbacks();
}
