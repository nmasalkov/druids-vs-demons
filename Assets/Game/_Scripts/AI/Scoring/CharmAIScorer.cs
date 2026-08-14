public class CharmAIScorer : ActionAIScorer
{
    public override int Score()
    {
        int baseScore = PlayerCreatureCount switch { 0 => 0, 1 => 10, 2 => 50, 3 => 95, _ => 100 };
        return ClampScore(baseScore - 5 * EnemyCreatureCount);
    }
}
