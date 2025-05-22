using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelTrainer.Services.Stores.Actions
{
    static class SessionActions
    {
        internal const string slice = "session";

        internal static readonly Creator deleteModel = new ($"{slice}/deleteModel");

        internal static readonly Creator addModel = new ($"{slice}/addModel");

        internal static readonly Creator<string> setBaseModel = new ($"{slice}/setBaseModel");

        internal static readonly Creator<(Setting setting, string value)> setUserSettingValue = new ($"{slice}/setUserSettingValue");

        internal static readonly Creator<string> setName = new ($"{slice}/setModelName");

        internal static readonly Creator addImage = new ($"{slice}/addImage");

        internal static readonly Creator<string> deleteImage = new ($"{slice}/deleteImage");

        internal static readonly Creator<string> addTag = new ($"{slice}/addTag");

        internal static readonly Creator<string> deleteTag = new ($"{slice}/deleteTag");

        internal static readonly Creator<string> selectModel = new ($"{slice}/setSelectedModel");

        internal static readonly Creator<string> setSearchFilter = new ($"{slice}/setSearchFilter");

        internal static readonly AsyncThunkCreatorWithPayload<BaseModel[]> fetchBaseModels =
            new ($"{slice}/fetchBaseModels", async _ => await FetchBaseModels());

        internal static readonly AsyncThunkCreator<UserModel,TrainingStatus> trainModel =
            new ($"{slice}/trainModel", TrainModel);

        static async Task<TrainingStatus> TrainModel(UserModel model, AsyncThunkApi<TrainingStatus> api)
        {
            var status = (TrainingStatus)Random.Range(2, 3);
            return await Task.FromResult(status);
        }

        static async Task<BaseModel[]> FetchBaseModels()
        {
            var taskID = Progress.Start($"Requesting base models.");
            try
            {
                void SetProgress(float progress, string description)
                {
                    if (taskID > 0)
                        Progress.Report(taskID, progress, description);
                }

                //todo: using var client = new ApiClient();

                SetProgress(0.0f, "Authenticating with UnityConnect.");
                while (string.IsNullOrEmpty(CloudProjectSettings.organizationKey))
                    await EditorTask.Yield();

                SetProgress(0.8f, "Receiving models.");
                var baseModels = new List<BaseModel>
                {
                    new BaseModel
                    {
                        id = "flux",
                        name = "Flux",
                        settings = new Setting[]
                        {
                            new Setting
                            {
                                name = "automaticTrainingSteps",
                                displayName = "Automatic Training Steps",
                                description = "Automatically determine the number of training steps based on the training data.",
                                type = SettingType.BoolValue,
                                defaultValue = "true",
                            },
                            new Setting
                            {
                                name = "totalTrainingSteps",
                                displayName = "Total Training Steps",
                                description = "The total number of training steps to run.",
                                type = SettingType.IntWithMinMaxValue,
                                defaultValue = "10",
                                config = new IntWithMinMaxConfig()
                                {
                                    minValue = 1,
                                    maxValue = 20
                                }
                            },
                            new Setting
                            {
                                name = "learningRate",
                                displayName = "Learning Rate",
                                description = "The learning rate for the model.",
                                type = SettingType.FloatWithMinMaxValue,
                                defaultValue = "0.001",
                                config = new FloatWithMinMaxConfig
                                {
                                    minValue = 0.0001f,
                                    maxValue = 1.0f
                                }
                            },
                            new Setting
                            {
                                name = "Optimize Portrait",
                                displayName = "Optimize Portrait",
                                description = "Optimize the model for portrait images.",
                                type = SettingType.BoolValue,
                                defaultValue = "false"
                            }
                        }
                    },
                    new BaseModel
                    {
                        id = "sdxl",
                        name = "SDXL",
                        settings = new Setting[]
                        {
                            new Setting
                            {
                                name = "automaticTrainingSteps",
                                displayName = "Automatic Training Steps",
                                description = "Automatically determine the number of training steps based on the training data.",
                                type = SettingType.BoolValue,
                                defaultValue = "true",
                            },
                            new Setting
                            {
                                name = "totalTrainingSteps",
                                displayName = "Total Training Steps",
                                description = "The total number of training steps to run.",
                                type = SettingType.IntWithMinMaxValue,
                                defaultValue = "10",
                                config = new IntWithMinMaxConfig()
                                {
                                    minValue = 1,
                                    maxValue = 20
                                }
                            },
                            new Setting
                            {
                                name = "learningRate",
                                displayName = "Learning Rate",
                                description = "The learning rate for the model.",
                                type = SettingType.FloatWithMinMaxValue,
                                defaultValue = "0.001",
                                config = new FloatWithMinMaxConfig
                                {
                                    minValue = 0.0001f,
                                    maxValue = 1.0f
                                }
                            },
                            new Setting
                            {
                                name = "textEncoderTrainingRatio",
                                displayName = "Text Encoder Training Ratio",
                                description = "The ratio of training steps to run on the text encoder.",
                                type = SettingType.FloatWithMinMaxValue,
                                defaultValue = "0.5",
                                config = new FloatWithMinMaxConfig
                                {
                                    minValue = 0.0001f,
                                    maxValue = 1.0f
                                }
                            },
                            new Setting
                            {
                                name = "imageEncoderLearningRate",
                                displayName = "Image Encoder Learning Rate",
                                description = "The rate of learning to run on the image encoder.",
                                type = SettingType.FloatWithMinMaxValue,
                                defaultValue = "0.5",
                                config = new FloatWithMinMaxConfig
                                {
                                    minValue = 0.0001f,
                                    maxValue = 1.0f
                                }
                            }
                        }
                    },
                    new BaseModel
                    {
                        id = "bria",
                        name = "Bria",
                        settings = new Setting[]
                        {
                            new Setting
                            {
                                name = "automaticTrainingSteps",
                                displayName = "Automatic Training Steps",
                                description = "Automatically determine the number of training steps based on the training data.",
                                type = SettingType.BoolValue,
                                defaultValue = "true",
                            },

                            new Setting
                            {
                                name = "totalTrainingSteps",
                                displayName = "Total Training Steps",
                                description = "The total number of training steps to run.",
                                type = SettingType.IntWithMinMaxValue,
                                defaultValue = "10",
                                config = new IntWithMinMaxConfig()
                                {
                                    minValue = 1,
                                    maxValue = 20
                                }
                            },

                            new Setting
                            {
                                name = "learningRate",
                                displayName = "Learning Rate",
                                description = "The learning rate for the model.",
                                type = SettingType.FloatWithMinMaxValue,
                                defaultValue = "0.001",
                                config = new FloatWithMinMaxConfig
                                {
                                    minValue = 0.0001f,
                                    maxValue = 1.0f
                                }
                            },

                            new Setting
                            {
                                name = "Optimize Portrait",
                                displayName = "Optimize Portrait",
                                description = "Optimize the model for portrait images.",
                                type = SettingType.BoolValue,
                                defaultValue = "false"
                            }
                        }
                    }
                };
                SetProgress(1, $"Retrieved {baseModels.Count} base models.");
                return baseModels.ToArray();
            }
            finally
            {
                Progress.Finish(taskID);
            }
        }
    }
}
