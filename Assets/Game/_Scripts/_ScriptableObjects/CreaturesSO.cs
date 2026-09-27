using UnityEngine;

[CreateAssetMenu(fileName = "DefaultCreatures", menuName = "Game/Defaults/Creatures")]
public class CreaturesSO : ScriptableObject
{
    public ArcherSO archer;
    public TankSO tank;
    public MageSO mage;
}

