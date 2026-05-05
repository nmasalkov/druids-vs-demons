using System.Collections.Generic;
using UnityEngine;

public class BalanceTool : MonoBehaviour
{
    public void SpawnPlayer(CreatureSO creature)
    {
        G.PlayerCreaturesManager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    public void SpawnEnemy(CreatureSO creature)
    {
        G.EnemyCreaturesManager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    public void SpawnPlayerMage() => SpawnPlayer(G.DefaultCreatures.mage);
    public void SpawnPlayerArcher() => SpawnPlayer(G.DefaultCreatures.archer);
    public void SpawnPlayerTank() => SpawnPlayer(G.DefaultCreatures.tank);

    public void SpawnEnemyMage() => SpawnEnemy(G.DefaultCreatures.mage);
    public void SpawnEnemyArcher() => SpawnEnemy(G.DefaultCreatures.archer);
    public void SpawnEnemyTank() => SpawnEnemy(G.DefaultCreatures.tank);

    public void PlayBattle()
    {
        new BattleState().BeginBattle();
    }
}
