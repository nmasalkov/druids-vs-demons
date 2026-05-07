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

        private Transform target;
        private float speed;
        private float reachThreshold = 0.2f;

        private GameObject spawnedProjectileParticle;
        private bool arrived;

        public void Launch(Transform target, float speed)
        {
            this.target = target;
            this.speed = speed;

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
            float distance = Vector3.Distance(transform.position, target.position);
            float duration = distance / speed;

            BallisticTrajectory.Apply(transform, target.position, duration, arcHeight)
                .OnComplete(OnArrived);
        }

        private void Update()
        {
            if (arrived || target == null) return;
            if (trajectoryType != TrajectoryType.Direct) return;

            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * (speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(direction);

            if (Vector3.Distance(transform.position, target.position) <= reachThreshold)
            {
                OnArrived();
            }
        }

        private void OnArrived()
        {
            arrived = true;

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


