using UnityEngine;

namespace SSP25DP1
{
    public class FlareDestroy : MonoBehaviour
    {
        // Destroy Instantiated Explosion GameObject
        [SerializeField] float deathTimer;
        void LateUpdate()
        {
            Destroy(gameObject, deathTimer);
        }
    }
}