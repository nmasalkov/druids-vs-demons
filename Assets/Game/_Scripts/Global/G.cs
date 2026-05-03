using UnityEngine;

public class G : MonoBehaviour
{
    public static G Instance { get; private set; }

    [SerializeField] private DefaultCreaturesSO defaultCreatures;

    public static DefaultCreaturesSO DefaultCreatures => Instance.defaultCreatures;

    void Awake()
    {
        Instance = this;
    }
}
