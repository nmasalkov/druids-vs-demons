using System;
using Game._Scripts.Creatures;

namespace _Scripts.Creatures
{
    public struct HitInfo
    {
        public Unit Target;
        public Action OnHit;
    }
}
