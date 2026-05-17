using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class UnitSlot : MonoBehaviour
    {
        [field: SerializeField] public Unit Unit { get; set; }
        [SerializeField] private Transform meleeAttackerPosition;

        public Transform MeleeAttackerPosition => meleeAttackerPosition;

        /// <summary>
        /// Convenience accessor. Returns Unit cast to Creature, or null.
        /// </summary>
        public Creature Creature
        {
            get => Unit as Creature;
            set => Unit = value;
        }
    }
}
