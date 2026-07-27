using UnityEngine;

/// <summary>
/// Debug-only live view of the entire RunState. While this component is enabled, every frame it
/// re-serializes CampaignStateManager.Instance.CurrentRun (every field, not a hand-picked subset)
/// into <see cref="serialized"/> so the whole persisted state is visible in the Inspector with no
/// resolve button and no field list to keep in sync as RunState grows. Disabled by default —
/// Unity skips a disabled component's Update() entirely, so there's no per-frame cost unless a
/// developer explicitly checks the enabled box while debugging. Lives as a child GameObject of
/// CampaignProgress (see docs/Campaign.md).
/// </summary>
public class RunStateMonitor : MonoBehaviour
{
    [TextArea(12, 40)]
    [SerializeField] private string serialized;

    void Update()
    {
        serialized = JsonUtility.ToJson(CampaignStateManager.Instance.CurrentRun, true);
    }
}
