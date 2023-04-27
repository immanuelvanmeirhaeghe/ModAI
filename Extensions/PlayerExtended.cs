using ModAI.Managers;
using UnityEngine;

namespace ModAI.Extensions
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModAI)}__").AddComponent<ModAI>();            
            new GameObject($"__{nameof(StylingManager)}__").AddComponent<StylingManager>();
        }
    }
}
