using Game._Scripts.Nukes;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShock", menuName = "Game/Actions/Nukes/Shock")]
public class ShockSO : NukeSO
{
    public override NukeResolver CreateResolver() => new ShockResolver();
    public override ActionAIScorer CreateAIScorer() => new ShockAIScorer();
}

