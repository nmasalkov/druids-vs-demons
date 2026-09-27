using UnityEngine;

/// <summary>
/// Scene-resident timing config for <see cref="SpellState"/>. Analogous to
/// <see cref="NukeStateManager"/>.
/// </summary>
public class SpellStateManager : MonoBehaviour
{
    public static SpellStateManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float pauseBetweenSpells = 0.4f;

    public float PauseBetweenSpells => pauseBetweenSpells;

    void Awake()
    {
        Instance = this;
    }
}

