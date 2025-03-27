using System;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.States
{
    [Serializable]
    record Session
    {
        public Settings settings = new();
    }
}
