using UnityEngine;

/// <summary>
/// Scene-resident timing config for <see cref="NukeState"/>. Analogous to
/// <see cref="PostBattleStateManager"/>.
/// </summary>
public class NukeStateManager : MonoBehaviour
{
    public static NukeStateManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float pauseBetweenNukes = 0.6f;

    public float PauseBetweenNukes => pauseBetweenNukes;

    void Awake()
    {
        Instance = this;
    }
}

