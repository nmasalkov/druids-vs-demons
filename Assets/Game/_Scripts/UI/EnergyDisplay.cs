using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the player's current reroll energy total and plays a feedback whenever it changes.
/// </summary>
public class EnergyDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private MMF_Player energyChangeFeedback;

    void Start()
    {
        UpdateText(EnergyController.Instance.CurrentEnergy);
        EnergyController.Instance.OnEnergyChanged += HandleEnergyChanged;
    }

    void OnDestroy()
    {
        if (EnergyController.Instance != null)
            EnergyController.Instance.OnEnergyChanged -= HandleEnergyChanged;
    }

    private void HandleEnergyChanged(int newEnergy)
    {
        UpdateText(newEnergy);
        energyChangeFeedback.PlayFeedbacks();
    }

    private void UpdateText(int energy)
    {
        energyText.text = energy.ToString();
    }
}
