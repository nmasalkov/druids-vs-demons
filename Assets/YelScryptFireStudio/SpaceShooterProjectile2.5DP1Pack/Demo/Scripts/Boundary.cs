using UnityEngine;

namespace SSP25DP1
{
    public class Boundary : MonoBehaviour
    {
        private void OnTriggerExit(Collider other)
        {
            Destroy(other.gameObject);
        }
    }
}