public class FireMagicAIScorer : ActionAIScorer
{
    public override int Score()
    {
        if (PlayerHasShield || PlayerHasTank) return 0;
        return PlayerCreatureCount == 0 ? EmptyBoardScore() : MageArcherScore();
    }

    private static int EmptyBoardScore()
    {
        if (PlayerHpPercentageBelow(15)) return 100;
        if (PlayerHpPercentageBelow(30)) return 90;
        return 50;
    }

    private static int MageArcherScore()
    {
        int score = 0;
        if (PlayerHasMage) score += 40 + (CreatureHpBelow(PlayerMage, 25) ? 50 : 0);
        if (PlayerHasArcher) score += 40 + (CreatureHpBelow(PlayerArcher, 25) ? 50 : 0);
        return ClampScore(score);
    }
}
