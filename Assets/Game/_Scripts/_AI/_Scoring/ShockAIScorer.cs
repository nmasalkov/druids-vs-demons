using Game._Scripts.Creatures;

public class ShockAIScorer : ActionAIScorer
{
    public override int Score()
    {
        if (PlayerHasShield) return 0;
        int total = 0;
        foreach (var creature in PlayerCreatures)
            total += ScoreForCreature(creature);
        return ClampScore(total);
    }

    private static int ScoreForCreature(Creature creature)
    {
        int level = creature.Experience.Level;
        if (level >= 3) return 60;
        if (level == 2) return 50;
        return 20;
    }
}
