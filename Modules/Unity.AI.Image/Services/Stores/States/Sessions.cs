using System;
using UnityEngine;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record Session
    {
        public Settings settings = new();
    }
}
