using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    public class PolygonSoundSpawnMod : MonoBehaviour
    {
        public GameObject prefabSound;
        public AudioSource audioSource;
        
        [Range(0.01f, 10f)]
        public float pitchRandomMultiplier = 1f;

        public void OnValidate()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            
            var polygonSoundSpawn = GetComponent<PolygonArsenal.PolygonSoundSpawn>();
            if (polygonSoundSpawn != null)
            {
                prefabSound = polygonSoundSpawn.prefabSound;
                pitchRandomMultiplier = polygonSoundSpawn.pitchRandomMultiplier;
                audioSource.clip = polygonSoundSpawn.prefabSound.GetComponent<AudioSource>().clip;
                audioSource.pitch *= Random.value < .5
                    ? Random.Range(1 / pitchRandomMultiplier, 1)
                    : Random.Range(1, pitchRandomMultiplier);
            }
        }
    }
}
