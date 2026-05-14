using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class CreatureSlot : MonoBehaviour
    {
        [field: SerializeField] public Creature Creature { get; set; }
        [SerializeField] private Transform meleeAttackerPosition;

        public Transform MeleeAttackerPosition => meleeAttackerPosition;
    }
}
