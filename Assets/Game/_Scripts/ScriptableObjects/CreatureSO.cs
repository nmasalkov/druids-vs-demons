using UnityEngine;

[CreateAssetMenu(fileName = "NewCreature", menuName = "Game/Actions/Creature")]
public class CreatureSO : ActionSO
{
    public GameObject creaturePrefab;
    public float damage = 10f;
    public float health = 100f;
}
