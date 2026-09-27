using TMPro;
using UnityEngine;

public class HealthTextController : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    public void SetValue(float current, float maximum)
    {
        text.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(maximum)}";
    }
}
