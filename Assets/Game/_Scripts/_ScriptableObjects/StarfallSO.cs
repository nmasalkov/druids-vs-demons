using Game._Scripts.Nukes;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStarfall", menuName = "Game/Actions/Nukes/Starfall")]
public class StarfallSO : NukeSO
{
    public override NukeResolver CreateResolver() => new StarfallResolver();
    public override ActionAIScorer CreateAIScorer() => new StarfallAIScorer();
}
