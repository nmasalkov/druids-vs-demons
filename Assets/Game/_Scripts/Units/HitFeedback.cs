using UnityEngine;

namespace Game._Scripts.Creatures
{
    /// <summary>
    /// Sibling helper on every <see cref="Unit"/> exposing the world anchor where projectiles
    /// and nuke effects should land. Lets each unit prefab tune its own visual hit point
    /// (chest / center / head) independent of the unit's pivot transform.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [field: SerializeField] public Transform HitPlacePosition { get; private set; }
    }
}

