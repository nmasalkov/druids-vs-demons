using DG.Tweening;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class SimpleProjectile : MonoBehaviour
    {
        [Header("Particles")]
        public GameObject projectileParticle;
        public GameObject muzzleParticle;
        public GameObject impactParticle;
        public GameObject[] trailParticles;

        [Header("Trajectory")]
        public TrajectoryType trajectoryType = TrajectoryType.Direct;
        public float arcHeight = 3f;

        private Vector3 targetPosition;
        private float speed;
        private float reachThreshold = 0.2f;

        private GameObject spawnedProjectileParticle;
        private bool arrived;
        private System.Action onHit;

        public void Launch(Vector3 targetPosition, float speed, System.Action onHit = null)
        {
            this.targetPosition = targetPosition;
            this.speed = speed;
            this.onHit = onHit;

            if (projectileParticle != null)
            {
                spawnedProjectileParticle = Instantiate(projectileParticle, transform.position, transform.rotation);
                spawnedProjectileParticle.transform.SetParent(transform);
            }

            if (muzzleParticle != null)
            {
                var muzzle = Instantiate(muzzleParticle, transform.position, transform.rotation);
                Destroy(muzzle, 1.5f);
            }

            if (trajectoryType == TrajectoryType.Ballistic)
            {
                LaunchBallistic();
            }
        }

        private void LaunchBallistic()
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            float duration = distance / speed;

            BallisticTrajectory.Apply(transform, targetPosition, duration, arcHeight)
                .OnComplete(OnArrived);
        }

        private void Update()
        {
            if (arrived) return;
            if (trajectoryType != TrajectoryType.Direct) return;

            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * (speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(direction);

            if (Vector3.Distance(transform.position, targetPosition) <= reachThreshold)
            {
                OnArrived();
            }
        }

        private void OnArrived()
        {
            arrived = true;
            onHit?.Invoke();

            if (impactParticle != null)
            {
                var impact = Instantiate(impactParticle, transform.position, Quaternion.identity);
                Destroy(impact, 3f);
            }

            // Detach trails so they fade out naturally
            if (trailParticles != null && spawnedProjectileParticle != null)
            {
                foreach (var trail in trailParticles)
                {
                    var found = spawnedProjectileParticle.transform.Find(trail.name);
                    if (found != null)
                    {
                        found.SetParent(null);
                        Destroy(found.gameObject, 2f);
                    }
                }
            }

            if (spawnedProjectileParticle != null)
            {
                Destroy(spawnedProjectileParticle);
            }

            Destroy(gameObject);
        }
    }
}
