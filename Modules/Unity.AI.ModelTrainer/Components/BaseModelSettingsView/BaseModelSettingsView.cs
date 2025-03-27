using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class BaseModelSettingsView : VisualElement
    {
        const string k_Uxml =
            "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/BaseModelSettingsView/BaseModelSettingsView.uxml";

        readonly Label m_Title;

        public override VisualElement contentContainer { get; }

        public BaseModelSettingsView()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            contentContainer = this.Q<VisualElement>("contentContainer");

            m_Title = this.Q<Label>("title");

            this.Use(SessionSelectors.SelectBaseModelId, OnBaseModelChanged);
            this.Use(SessionSelectors.SelectSettings, OnSettingsChanged);
        }

        void OnSettingsChanged(IEnumerable<UserSetting> settings)
        {
            if (settings != null)
            {
                foreach (var setting in settings)
                {
                    var field = contentContainer.Children()
                        .FirstOrDefault(e => e.userData is Setting s && s.name == setting.name);
                    if (field == null)
                    {
                        // Debug.LogWarning($"Setting {setting.name} not found in view");
                    }
                    else
                    {
                        switch (field)
                        {
                            case INotifyValueChanged<bool> toggle:
                                toggle.SetValueWithoutNotify(setting.value == "True");
                                break;
                            case INotifyValueChanged<int> intField:
                                intField.SetValueWithoutNotify(int.TryParse(setting.value, out var iv) ? iv : 0);
                                break;
                            case INotifyValueChanged<float> floatField:
                                floatField.SetValueWithoutNotify(float.TryParse(setting.value, out var fv) ? fv : 0);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        void OnBaseModelChanged(string id)
        {
            UnbindAll();
            Clear();

            if (string.IsNullOrEmpty(id) || this.GetState() is not {} state)
                return;

            var baseModel = state.SelectBaseModel(id);
            if (baseModel != null)
            {
                m_Title.text = $"{baseModel.name} Options";
                foreach (var setting in baseModel.settings)
                {
                    var field = MakeSettingField(setting);
                    contentContainer.Add(field);
                }
            }
        }

        void UnbindAll()
        {
            foreach (var element in Children())
            {
                switch (element)
                {
                    case INotifyValueChanged<bool> toggle:
                        toggle.UnregisterValueChangedCallback(OnToggleChanged);
                        break;
                    case INotifyValueChanged<int> intField:
                        intField.UnregisterValueChangedCallback(OnIntFieldChanged);
                        break;
                    case INotifyValueChanged<float> floatField:
                        floatField.UnregisterValueChangedCallback(OnFloatFieldChanged);
                        break;
                    default:
                        break;
                }
                element.userData = null;
            }
        }

        static void OnToggleChanged(ChangeEvent<bool> evt)
        {
            if (evt.target is VisualElement { userData: Setting setting } element)
                element.GetStoreApi().Dispatch(SessionActions.setUserSettingValue.Invoke((setting, evt.newValue.ToString())));
        }

        static void OnIntFieldChanged(ChangeEvent<int> evt)
        {
            if (evt.target is VisualElement { userData: Setting setting } element)
                element.GetStoreApi().Dispatch(SessionActions.setUserSettingValue.Invoke((setting, evt.newValue.ToString(CultureInfo.InvariantCulture))));
        }

        static void OnFloatFieldChanged(ChangeEvent<float> evt)
        {
            if (evt.target is VisualElement { userData: Setting setting } element)
                element.GetStoreApi().Dispatch(SessionActions.setUserSettingValue.Invoke((setting, evt.newValue.ToString(CultureInfo.InvariantCulture))));
        }

        static VisualElement MakeSettingField(Setting setting)
        {
            return setting.type switch
            {
                SettingType.BoolValue => MakeToggle(setting),
                SettingType.IntValue => MakeIntField(setting),
                SettingType.FloatValue => MakeFloatField(setting),
                SettingType.IntWithMinMaxValue => MakeSliderInt(setting, setting.config as IntWithMinMaxConfig),
                SettingType.FloatWithMinMaxValue => MakeSlider(setting, setting.config as FloatWithMinMaxConfig),
                _ => throw new System.NotImplementedException("Unknown setting type " + setting.type)
            };
        }

        static VisualElement MakeIntField(Setting setting)
        {
            var field = new IntegerField(setting.displayName)
            {
                userData = setting
            };
            field.AddToClassList("options-view__row");
            field.AddToClassList("options-view__input-field");
            var val = int.TryParse(setting.defaultValue, out var v) ? v : 0;
            field.SetValueWithoutNotify(val);
            field.RegisterValueChangedCallback(OnIntFieldChanged);
            return field;
        }

        static VisualElement MakeFloatField(Setting setting)
        {
            var field = new FloatField(setting.displayName)
            {
                userData = setting
            };
            field.AddToClassList("options-view__row");
            field.AddToClassList("options-view__input-field");
            var val = float.TryParse(setting.defaultValue, out var v) ? v : 0;
            field.SetValueWithoutNotify(val);
            field.RegisterValueChangedCallback(OnFloatFieldChanged);
            return field;
        }

        static VisualElement MakeSlider(Setting setting, FloatWithMinMaxConfig cfg)
        {
            cfg ??= new FloatWithMinMaxConfig {minValue = 0, maxValue = 1};
            var slider = new Slider(cfg.minValue, cfg.maxValue, SliderDirection.Horizontal)
            {
                showInputField = true,
                label = setting.displayName,
                userData = setting
            };
            slider.AddToClassList("options-view__row");
            slider.AddToClassList("options-view__slider");
            var val = float.TryParse(setting.defaultValue, out var v) ? v : cfg.minValue;
            slider.SetValueWithoutNotify(val);
            slider.RegisterValueChangedCallback(OnFloatFieldChanged);
            return slider;
        }

        static VisualElement MakeSliderInt(Setting setting, IntWithMinMaxConfig cfg)
        {
            cfg ??= new IntWithMinMaxConfig { minValue = 0, maxValue = 100 };
            var slider = new SliderInt(cfg.minValue, cfg.maxValue, SliderDirection.Horizontal)
            {
                showInputField = true,
                label = setting.displayName,
                userData = setting
            };
            slider.AddToClassList("options-view__row");
            slider.AddToClassList("options-view__slider");
            var val = int.TryParse(setting.defaultValue, out var v) ? v : cfg.minValue;
            slider.SetValueWithoutNotify(val);
            slider.RegisterValueChangedCallback(OnIntFieldChanged);
            return slider;
        }

        static VisualElement MakeToggle(Setting setting)
        {
            var toggle = new Toggle(setting.displayName)
            {
                userData = setting
            };
            toggle.AddToClassList("options-view__row");
            toggle.AddToClassList("options-view__toggle");
            toggle.SetValueWithoutNotify(setting.defaultValue == "True");
            toggle.RegisterValueChangedCallback(OnToggleChanged);
            return toggle;
        }
    }
}
