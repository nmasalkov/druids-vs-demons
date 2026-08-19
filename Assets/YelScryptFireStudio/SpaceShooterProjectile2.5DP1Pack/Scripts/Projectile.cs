using UnityEngine;

namespace SSP25DP1
{
    public class Projectile : MonoBehaviour
    {
        #region VARIABLES
        public enum Targets { Enemy, Player }
        private enum ProjectileParts { Projectile, Trail, Waves, Sparks }

        [SerializeField] private float speed;
        [SerializeField] private GameObject hitVFX;
        [SerializeField] private Targets targets;

        // Rigibody variable
        private Rigidbody rbProjectile;
        #endregion

        #region EXECUTION
        private void Awake()
        {
            // Gets the Rigidbody
            rbProjectile = GetComponent<Rigidbody>();
        }

        void Start()
        {
            // Moves the Projectile by Rigidbody Velocity Physics
            rbProjectile.linearVelocity = transform.forward * speed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targets.ToString()))
            {
                ExplosionVFX();
            }
        }
        #endregion

        #region HIT EXPLOSION VFX
        private void ExplosionVFX()
        {
            // Instance the Explosion GameObject
            Instantiate(hitVFX, transform.position, Quaternion.identity);

            // Disables the Collider Component
            gameObject.GetComponent<Collider>().enabled = false;

            // Gets the Children of the Instantiated GameObject
            Transform[] gameObjectChildren = gameObject.GetComponentsInChildren<Transform>();

            // Variable to be used to assign the value of the Emission.main.duration of the particle.
            float emissionDuration = 0f;

            // Step-by-Step Process to Destroy Child GameObjects and Stop Particle Emission
            foreach (var child in gameObjectChildren)
            {
                if (child.gameObject.name == ProjectileParts.Projectile.ToString() || child.gameObject.name == ProjectileParts.Trail.ToString())
                {
                    Destroy(child.gameObject);
                }
                else if (child.gameObject.name == ProjectileParts.Waves.ToString() || child.gameObject.name == ProjectileParts.Sparks.ToString())
                {
                    ParticleSystem emissionSparkless = child.GetComponent<ParticleSystem>();
                    emissionDuration = emissionSparkless.main.duration;
                    emissionSparkless.Stop();
                }
            }

            // This destroy the projectiles
            Destroy(gameObject, emissionDuration);
        }
        #endregion
    }
}
