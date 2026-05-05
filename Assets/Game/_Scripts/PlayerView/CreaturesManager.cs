using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class CreaturesManager : MonoBehaviour
    {
        [SerializeField] private Transform magePosition;
        [SerializeField] private Transform archerPosition;
        [SerializeField] private Transform tankPosition;

        [field: SerializeField] public Creature Mage { get; private set; }
        [field: SerializeField] public Creature Archer { get; private set; }
        [field: SerializeField] public Creature Tank { get; private set; }

        public List<Creature> GetAllCreatures()
        {
            var list = new List<Creature>();
            if (Tank != null) list.Add(Tank);
            if (Mage != null) list.Add(Mage);
            if (Archer != null) list.Add(Archer);
            return list;
        }

        public void SpawnCreatures(List<CreatureSO> creatures)
        {
            foreach (var creatureSO in creatures)
            {
                switch (creatureSO)
                {
                    case MageSO when Mage == null:
                        Mage = Instantiate(creatureSO.creaturePrefab, magePosition).GetComponent<Creature>();
                        break;
                    case ArcherSO when Archer == null:
                        Archer = Instantiate(creatureSO.creaturePrefab, archerPosition).GetComponent<Creature>();
                        break;
                    case TankSO when Tank == null:
                        Tank = Instantiate(creatureSO.creaturePrefab, tankPosition).GetComponent<Creature>();
                        break;
                }
            }
        }
    }
}
