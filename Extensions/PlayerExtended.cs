using UnityEngine;

namespace ModAI
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModAI)}__").AddComponent<ModAI>();
        }
    }
}
