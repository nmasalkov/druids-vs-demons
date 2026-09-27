using UnityEngine;

public class PostBattleStateManager : MonoBehaviour
{
    public static PostBattleStateManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float cleanUpDelay = 0.5f;
    [SerializeField] private float gemCollectionDelay = 0.5f;

    public float CleanUpDelay => cleanUpDelay;
    public float GemCollectionDelay => gemCollectionDelay;

    void Awake()
    {
        Instance = this;
    }
}

