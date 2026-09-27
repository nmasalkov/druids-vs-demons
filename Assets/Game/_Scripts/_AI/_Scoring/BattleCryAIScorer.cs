using UnityEngine;

public class BattleCryAIScorer : ActionAIScorer
{
    public override int Score()
    {
        int score = Mathf.Min(20 * PlayerCreatureCount + 20 * EnemyCreatureCount, 100);
        return EnemyCreatureCount == 1 ? Mathf.Min(score, 50) : score;
    }
}
