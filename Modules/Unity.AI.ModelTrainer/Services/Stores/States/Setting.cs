using System;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record Setting
    {
        public string name;
        public string displayName;
        public string description;
        public SettingType type;
        public string defaultValue;
        public Config config;
    }

    enum SettingType
    {
        BoolValue,

        IntValue,
        IntWithMinValue,
        IntWithMaxValue,
        IntWithMinMaxValue,

        FloatValue,
        FloatWithMinValue,
        FloatWithMaxValue,
        FloatWithMinMaxValue,
    }

    [Serializable]
    record Config { }

    [Serializable]
    record FloatWithMinMaxConfig : Config
    {
        public float minValue;
        public float maxValue;
    }

    [Serializable]
    record IntWithMinMaxConfig : Config
    {
        public int minValue;
        public int maxValue;
    }
}
