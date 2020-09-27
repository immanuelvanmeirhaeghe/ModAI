using Enums;
using UnityEngine;

namespace ModAI
{
    class TrapExtended : Trap
    {
        protected override void Start()
        {
             if (m_Info.m_ID == ItemID.Frog_Stretcher)
            {
                m_ArmSoundClips.Add((AudioClip)Resources.Load("Sounds/Traps/snare_trap_arm_02"));
                m_ArmSoundClips.Add((AudioClip)Resources.Load("Sounds/Traps/snare_trap_arm_03"));
            }
            base.Start();
        }
    }
}
