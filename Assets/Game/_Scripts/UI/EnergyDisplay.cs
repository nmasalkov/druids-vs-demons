/// <summary>
/// Shows the player's current reroll energy total — see docs/Energy.md.
/// </summary>
public class EnergyDisplay : RerollResourceDisplay
{
    protected override int CurrentValue => EnergyController.Instance.CurrentEnergy;

    protected override void Subscribe()
    {
        EnergyController.Instance.OnEnergyChanged += UpdateText;
        EnergyController.Instance.OnEnergySpent += PlayChangeFeedback;
    }

    protected override void Unsubscribe()
    {
        if (EnergyController.Instance == null) return;
        EnergyController.Instance.OnEnergyChanged -= UpdateText;
        EnergyController.Instance.OnEnergySpent -= PlayChangeFeedback;
    }
}
