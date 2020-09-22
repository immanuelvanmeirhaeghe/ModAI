using Enums;
using UnityEngine;

namespace ModAI
{
    class AIExtended : AIs.AI
    {
        protected override void UpdateSwimming()
        {
            if ((ModAI.Get().IsModActiveForSingleplayer || ModAI.Get().IsModActiveForMultiplayer) && ModAI.Get().CanSwimOption)
            {
                if (!IsDead() && (IsCat() || IsEnemy() || IsPredator()))
                {
                    m_Params.m_CanSwim = true;
                }
            }
            base.UpdateSwimming();
        }
    }
}
