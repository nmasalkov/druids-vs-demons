using DG.Tweening;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class SimpleProjectile : MonoBehaviour
    {
        [Header("Particles")]
        [Tooltip("The in-flight visual (particle system or sprite), instantiated as a child of this " +
                 "projectile so it follows its position and rotation. A sprite-based visual needs " +
                 "BillboardCorrector on it to stay flat to the camera.")]
        public GameObject projectileParticle;

        [Tooltip("One-shot effect spawned at the launch point, destroyed after 1.5s.")]
        public GameObject muzzleParticle;

        [Tooltip("One-shot effect spawned where the projectile arrives, destroyed after 3s.")]
        public GameObject impactParticle;

        [Tooltip("Children of the spawned in-flight visual (matched by name) that get detached on " +
                 "arrival and destroyed after 2s, so their trails fade out instead of vanishing.")]
        public GameObject[] trailParticles;

        [Header("Trajectory")]
        [Tooltip("Direct flies straight at the target. Ballistic follows an arc (see Arc Height) and " +
                 "can spin (see Spin Rotations Per Second). Flight time is distance / the firing " +
                 "animator's projectile speed either way.")]
        public TrajectoryType trajectoryType = TrajectoryType.Direct;

        [Tooltip("Ballistic only. World units the arc's bezier control point is raised above the " +
                 "midpoint between launch and target — the visible apex ends up at half this height.")]
        public float arcHeight = 3f;

        [Tooltip("Ballistic only. Full turns per second the projectile spins around the camera axis " +
                 "while flying (1 = 360°/s). Positive tumbles forward — the top of the art rolls " +
                 "toward the target — mirrored automatically for shots flying left; negative " +
                 "backspins. 0 = no spin. Works for sprite visuals (via BillboardCorrector) and " +
                 "particle visuals (the whole emitter turns).")]
        public float spinRotationsPerSecond;

        /// <summary>Unit direction the projectile is currently travelling in.</summary>
        public Vector3 TravelDirection { get; private set; }

        /// <summary>Current spin around the camera axis, in degrees (0 unless a ballistic
        /// projectile has a non-zero spin). Already applied to this transform's rotation, on top of
        /// the aim along <see cref="TravelDirection"/>.</summary>
        public float SpinAngle { get; private set; }

        private const float MinBallisticStepSqr = 0.0001f;

        private Vector3 targetPosition;
        private float speed;
        private float reachThreshold = 0.2f;

        private GameObject spawnedProjectileParticle;
        private bool arrived;
        private System.Action onHit;

        private Tween ballisticFlight;
        private Vector3 previousPosition;
        private float spinDegreesPerSecond;

        public void Launch(Vector3 targetPosition, float speed, System.Action onHit = null)
        {
            this.targetPosition = targetPosition;
            this.speed = speed;
            this.onHit = onHit;
            TravelDirection = (targetPosition - transform.position).normalized;

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
            spinDegreesPerSecond = ForwardTumbleSign() * spinRotationsPerSecond * 360f;
            previousPosition = transform.position;

            ballisticFlight = BallisticTrajectory.Apply(transform, targetPosition, duration, arcHeight)
                .OnUpdate(FaceBallisticTravel)
                .OnComplete(OnArrived);
        }

        /// <summary>Screen-space Z rotation is counter-clockwise for positive angles, so a shot
        /// flying right needs a negative spin for its top to roll toward the target.</summary>
        private float ForwardTumbleSign() => targetPosition.x >= transform.position.x ? -1f : 1f;

        /// <summary>Runs right after BallisticTrajectory moves the transform each tween update: aims
        /// along the arc's tangent, then spins around the camera axis on top of that aim.</summary>
        private void FaceBallisticTravel()
        {
            Vector3 step = transform.position - previousPosition;
            previousPosition = transform.position;
            if (step.sqrMagnitude > MinBallisticStepSqr) TravelDirection = step.normalized;

            SpinAngle = spinDegreesPerSecond * ballisticFlight.Elapsed();
            transform.rotation = Quaternion.AngleAxis(SpinAngle, Vector3.forward)
                                 * Quaternion.LookRotation(TravelDirection);
        }

        private void Update()
        {
            if (arrived) return;
            if (trajectoryType != TrajectoryType.Direct) return;

            Vector3 direction = (targetPosition - transform.position).normalized;
            TravelDirection = direction;
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
