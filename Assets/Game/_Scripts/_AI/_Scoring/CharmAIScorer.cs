using UnityEngine;

/// <summary>
/// How badly the AI wants to cast Charm this turn. Two factors, in order: how much there is worth
/// stealing (the player's board), then how much the AI's own creature advantage is hurting its odds
/// — the same penalty CharmResolver applies to the real roll, read off the same CharmSO tunable so
/// the AI can't keep prioritising a steal whose chance has quietly collapsed. See docs/AI.md.
/// </summary>
public class CharmAIScorer : ActionAIScorer
{
    private readonly CharmSO _source;

    public CharmAIScorer(CharmSO source)
    {
        _source = source;
    }

    public override int Score()
    {
        if (PlayerCreatureCount == 0) return 0;                       // nothing to steal
        return ClampScore(Mathf.RoundToInt(StealValue() * AdvantagePenalty()));
    }

    /// <summary>Raw appetite from the size of the player's board — the pool Charm steals from.</summary>
    private static int StealValue() => PlayerCreatureCount switch { 1 => 10, 2 => 50, 3 => 95, _ => 100 };

    /// <summary>
    /// Scales that appetite by the AI's own creature advantage, mirroring the resolver's odds.
    /// Replaces an older flat "-5 per own creature" term, which penalised simply having a board
    /// rather than out-numbering the player — the thing that actually cuts the success chance.
    /// </summary>
    private float AdvantagePenalty() =>
        _source.GetCreatureAdvantageMultiplier(EnemyCreatureCount, PlayerCreatureCount);
}
