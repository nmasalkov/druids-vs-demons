using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DefaultSpells", menuName = "Game/Defaults/Spells")]
public class DefaultSpellsSO : ScriptableObject
{
    public List<SpellSO> spells;
}

