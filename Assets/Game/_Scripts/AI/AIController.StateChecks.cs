using System.Linq;

public partial class AIController
{
    public static float PlayerHeroHealthPercent => G.PlayerHero.Health.HealthPercent;
    public static float EnemyHeroHealthPercent => G.EnemyHero.Health.HealthPercent;

    public static bool PlayerHeroBelow(float thresholdPercent01) => PlayerHeroHealthPercent < thresholdPercent01;
    public static bool EnemyHeroBelow(float thresholdPercent01) => EnemyHeroHealthPercent < thresholdPercent01;

    public static bool AnyPlayerCreatureBelow(float thresholdPercent01) =>
        G.PlayerCreaturesManager.GetAllCreatures().Any(c => c.Health.HealthPercent < thresholdPercent01);
    public static bool AnyEnemyCreatureBelow(float thresholdPercent01) =>
        G.EnemyCreaturesManager.GetAllCreatures().Any(c => c.Health.HealthPercent < thresholdPercent01);

    public static int EnemyCreatureCount => G.EnemyCreaturesManager.GetAllCreatures().Count;
    public static int PlayerCreatureCount => G.PlayerCreaturesManager.GetAllCreatures().Count;

    public static bool EnemyBoardFull =>
        G.EnemyCreaturesManager.Tank != null && G.EnemyCreaturesManager.Archer != null && G.EnemyCreaturesManager.Mage != null;
}
