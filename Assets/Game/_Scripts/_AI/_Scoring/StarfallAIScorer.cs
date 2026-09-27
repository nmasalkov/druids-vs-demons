public class StarfallAIScorer : ActionAIScorer
{
    public override int Score()
    {
        int score = HpTierScore();
        if (PlayerHasShield && PlayerCreatureCount > 0) score += 20 * PlayerCreatureCount;
        score += 30 * CountPlayerCreaturesBelow(30);
        return ClampScore(score);
    }

    private static int HpTierScore()
    {
        if (PlayerHpPercentageBelow(15)) return 85;
        if (PlayerHpPercentageBelow(25)) return 70;
        return 0;
    }
}
