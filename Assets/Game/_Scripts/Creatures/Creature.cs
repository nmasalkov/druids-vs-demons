using _Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(CreatureAnimator))]
    public class Creature : MonoBehaviour
    {
        public CreatureAnimator Animator { get; private set; }

        void Awake()
        {
            Animator = GetComponent<CreatureAnimator>();
        }
    }
}