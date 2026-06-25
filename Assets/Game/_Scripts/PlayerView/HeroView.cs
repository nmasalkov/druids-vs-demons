using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class HeroView : MonoBehaviour
    {
        [SerializeField] private Hero hero;
        [SerializeField] private CreaturesManager creaturesManager;
        [SerializeField] private UnitSlot heroSlot;
        [SerializeField] private UnitSlot shieldSlot;

        public Hero Hero => hero;
        public CreaturesManager CreaturesManager => creaturesManager;
        public UnitSlot HeroSlot => heroSlot;
        public UnitSlot ShieldSlot => shieldSlot;
        public Shield Shield => shieldSlot.Unit as Shield;

        void Start()
        {
            hero.Slot = heroSlot;
        }
    }
}

