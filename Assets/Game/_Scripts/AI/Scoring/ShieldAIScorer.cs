public class ShieldAIScorer : ActionAIScorer
{
    public override int Score()
    {
        int score = 0;
        if (EnemyHasTank) score += 10;
        if (EnemyHasMage) score += 20;
        if (EnemyHasArcher) score += 25;
        score += 10 * CountEnemyCreaturesBelow(50);
        if (EnemyHpPercentageBelow(30)) score += 15;
        return ClampScore(score);
    }
}
