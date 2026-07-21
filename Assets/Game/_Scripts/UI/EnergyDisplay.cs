using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the player's current reroll energy total and plays a feedback whenever the player
/// actually spends energy on a reroll. Text updates on every energy change (including
/// campaign-driven ones — initial load, encounter transition, restart), but the feedback only
/// plays for real spending (EnergyController.OnEnergySpent) — see docs/Energy.md.
/// </summary>
public class EnergyDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private MMF_Player energyChangeFeedback;

    void Start()
    {
        UpdateText(EnergyController.Instance.CurrentEnergy);
        EnergyController.Instance.OnEnergyChanged += HandleEnergyChanged;
        EnergyController.Instance.OnEnergySpent += PlayChangeFeedback;
    }

    void OnDestroy()
    {
        if (EnergyController.Instance != null)
        {
            EnergyController.Instance.OnEnergyChanged -= HandleEnergyChanged;
            EnergyController.Instance.OnEnergySpent -= PlayChangeFeedback;
        }
    }

    private void HandleEnergyChanged(int newEnergy)
    {
        UpdateText(newEnergy);
    }

    private void PlayChangeFeedback()
    {
        energyChangeFeedback.PlayFeedbacks();
    }

    private void UpdateText(int energy)
    {
        energyText.text = energy.ToString();
    }
}
