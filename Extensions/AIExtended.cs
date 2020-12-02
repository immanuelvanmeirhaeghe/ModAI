using Enums;
using UnityEngine;

namespace ModAI
{
    class AIExtended : AIs.AI
    {
        protected override void UpdateSwimming()
        {
            if (ModAI.Get().IsModActiveForSingleplayer || ModAI.Get().IsModActiveForMultiplayer)
            {
                if (!IsDead() && (IsCat() || IsEnemy() || IsPredator()))
                {
                    if (ModAI.Get().CanSwim)
                    {
                        m_Params.m_CanSwim = true;
                    }

                    if ((bool)m_HostileStateModule)
                    {
                        if (ModAI.Get().IsHostile)
                        {
                            m_HostileStateModule.m_State = AIs.HostileStateModule.State.Aggressive;
                        }
                        else
                        {
                            m_HostileStateModule.m_State = AIs.HostileStateModule.State.Calm;
                        }
                        m_HostileStateModule.OnUpdate();
                    }
                }
            }
            base.UpdateSwimming();
        }
    }
}
