/// <summary>
/// Shows the enemy AI's remaining fight-wide reroll pool — see docs/AI.md's "Rerolls" section.
/// </summary>
public class EnemyRerollDisplay : RerollResourceDisplay
{
    protected override int CurrentValue => AIController.RerollsRemaining;

    protected override void Subscribe()
    {
        AIController.Instance.OnRerollsChanged += UpdateText;
        AIController.Instance.OnRerollSpent += PlayChangeFeedback;
    }

    protected override void Unsubscribe()
    {
        if (AIController.Instance == null) return;
        AIController.Instance.OnRerollsChanged -= UpdateText;
        AIController.Instance.OnRerollSpent -= PlayChangeFeedback;
    }
}
