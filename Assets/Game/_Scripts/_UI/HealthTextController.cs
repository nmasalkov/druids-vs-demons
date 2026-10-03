using TMPro;
using UnityEngine;

public class HealthTextController : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    public void SetValue(float current, float maximum)
    {
        text.text = $"{DisplayedCurrent(current)}/{Mathf.RoundToInt(maximum)}";
    }

    // Anything still alive shows at least 1 — plain rounding showed e.g. 0.3 HP as "0".
    private static int DisplayedCurrent(float current)
        => current > 0f ? Mathf.Max(1, Mathf.RoundToInt(current)) : 0;
}
