using Game._Scripts.Creatures;
using Game._Scripts.Global;
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
            GameManager.OnBattleRestart += ClearShield;
        }

        void OnDestroy()
        {
            GameManager.OnBattleRestart -= ClearShield;
        }

        public void ClearShield()
        {
            if (Shield == null) return;
            Destroy(Shield.gameObject);
            ShieldSlot.Unit = null;
        }

        /// <summary>
        /// Destroys the current hero avatar and instantiates avatarPrefab in its place, preserving
        /// its local transform (keeps enemy-side mirroring intact) and rewiring hero/heroSlot.Unit.
        /// Called from CampaignManager.Start() (SEO -100) before the old avatar's own Start() would
        /// otherwise fire. See docs/Encounters.md.
        /// </summary>
        public void ReplaceHeroAvatar(GameObject avatarPrefab)
        {
            var oldGO = hero.gameObject;
            var parent = oldGO.transform.parent;
            var localPosition = oldGO.transform.localPosition;
            var localRotation = oldGO.transform.localRotation;
            var localScale = oldGO.transform.localScale;
            Destroy(oldGO);

            var newGO = Instantiate(avatarPrefab, parent);
            newGO.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            newGO.transform.localScale = localScale;

            hero = newGO.GetComponent<Hero>();
            hero.Slot = heroSlot;
            heroSlot.Unit = hero;
        }
    }
}

