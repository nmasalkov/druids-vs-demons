using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;

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

    /// <summary>The AI's own native-slot creatures currently stunned by Shock. Charm-slot occupants
    /// don't count — only a native slot's own roll can repair (heal/promote) it, see SpawningState.</summary>
    public static IReadOnlyList<Creature> ShockedEnemyCreatures
    {
        get
        {
            var manager = G.EnemyCreaturesManager;
            var list = new List<Creature>();
            if (manager.Tank != null && manager.Tank.StatusesManager.IsShocked) list.Add(manager.Tank);
            if (manager.Archer != null && manager.Archer.StatusesManager.IsShocked) list.Add(manager.Archer);
            if (manager.Mage != null && manager.Mage.StatusesManager.IsShocked) list.Add(manager.Mage);
            return list;
        }
    }
}
