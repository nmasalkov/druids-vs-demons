using System;
using Game._Scripts.Creatures;

namespace _Scripts.Creatures
{
    public struct HitInfo
    {
        public Targetable Target;
        public Action OnHit;
    }
}
