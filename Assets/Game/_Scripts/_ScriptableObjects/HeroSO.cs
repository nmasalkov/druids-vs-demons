using UnityEngine;

[CreateAssetMenu(fileName = "NewHero", menuName = "Game/Hero")]
public class HeroSO : ScriptableObject
{
    public GameObject heroPrefab;

    [Header("Stats")]
    public float health = 100f;
}

