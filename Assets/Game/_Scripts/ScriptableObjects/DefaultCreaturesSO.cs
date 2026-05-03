using UnityEngine;

[CreateAssetMenu(fileName = "DefaultCreatures", menuName = "Game/Defaults/Creatures")]
public class DefaultCreaturesSO : ScriptableObject
{
    public ArcherSO archer;
    public TankSO tank;
    public MageSO mage;
}

