using UnityEngine;

/// <summary>
/// Presentation timings for <see cref="BossPhaseTransitionState"/>. A GameState is a plain C# class
/// and can't carry serialized fields, so its tunables live here on a scene singleton — the same
/// arrangement PostBattleStateManager already uses for PostBattleState. Balance-free: every value is
/// pacing only, nothing here changes the transition's outcome (rule 7). See docs/GameLoop.md.
/// </summary>
public class BossPhaseTransitionManager : MonoBehaviour
{
    public static BossPhaseTransitionManager Instance { get; private set; }

    [Header("Timing")]
    [Tooltip("Seconds between the killing blow (when the boss's own creatures die with him) and the " +
             "start of his Rise animation. The 'everything has gone quiet' beat before he gets back up.")]
    [SerializeField] private float riseDelay = 3f;

    [Tooltip("Seconds after the resurrection feedback starts before the enemy side is actually swapped " +
             "to the next phase. Must land while the flash is at full white so the avatar substitution " +
             "is hidden — roughly half of the MMF_Flash's Flash Duration on " +
             "EnemyView/AvatarPosition/BossResurrectionFeedback (0.5s by default, so ~0.25 here).")]
    [SerializeField] private float flashToSwapDelay = 0.25f;

    [Tooltip("Seconds the new boss stands alone after the swap before the player's creatures are " +
             "killed. Long enough for the player to register the new form before the field resets.")]
    [SerializeField] private float boardSwapDelay = 2f;

    [Tooltip("Seconds the field stays empty after the player's dead creatures are cleared (which " +
             "itself waits for their full death animation plus PostBattleStateManager's clean-up " +
             "delay) and before the new phase's trio is summoned.")]
    [SerializeField] private float emptyBoardDelay = 0.5f;

    [Tooltip("Seconds after the new phase's trio is summoned before the transition ends and the " +
             "player's turn starts — lets the summon feedback land first.")]
    [SerializeField] private float postSummonDelay = 0.75f;

    public float RiseDelay => riseDelay;
    public float FlashToSwapDelay => flashToSwapDelay;
    public float BoardSwapDelay => boardSwapDelay;
    public float EmptyBoardDelay => emptyBoardDelay;
    public float PostSummonDelay => postSummonDelay;

    void Awake()
    {
        Instance = this;
    }
}
