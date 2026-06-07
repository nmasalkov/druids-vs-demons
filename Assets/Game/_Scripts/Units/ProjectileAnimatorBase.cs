using UnityEngine;

namespace _Scripts.Creatures
{
    public abstract class ProjectileAnimatorBase : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private SimpleProjectile projectilePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float projectileSpeed = 10f;

        protected void FireProjectile(Vector3 targetPosition, System.Action onHit = null)
        {
            Vector3 origin = spawnPoint.position;
            Vector3 direction = (targetPosition - origin).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);

            SimpleProjectile projectile = Instantiate(projectilePrefab, origin, rotation);
            projectile.Launch(targetPosition, projectileSpeed, onHit);
        }
    }
}
