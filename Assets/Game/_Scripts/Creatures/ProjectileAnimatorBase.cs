using UnityEngine;

namespace _Scripts.Creatures
{
    public abstract class ProjectileAnimatorBase : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private SimpleProjectile projectilePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float projectileSpeed = 10f;

        protected void FireProjectile(Transform target, System.Action onHit = null)
        {
            Vector3 origin = spawnPoint.position;
            Vector3 direction = (target.position - origin).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);

            SimpleProjectile projectile = Instantiate(projectilePrefab, origin, rotation);
            projectile.Launch(target, projectileSpeed, onHit);
        }
    }
}

