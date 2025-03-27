﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record Settings
    {
        public List<ModelSettings> models = new();
        public string environment = "";
    }
}
