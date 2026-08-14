using Game._Scripts.Nukes;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFireMagic", menuName = "Game/Actions/Nukes/Fire Magic")]
public class FireMagicSO : NukeSO
{
    public override NukeResolver CreateResolver() => new FireMagicResolver();
    public override ActionAIScorer CreateAIScorer() => new FireMagicAIScorer();
}
