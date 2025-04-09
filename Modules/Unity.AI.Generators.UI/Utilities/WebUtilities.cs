using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class WebUtilities
    {
        static readonly Dictionary<AssetReference, TaskCompletionSource<bool>> k_AssetCancellationDict = new();

        public static async Task<bool> WaitForCloudProjectSettings(AssetReference asset)
        {
            if (k_AssetCancellationDict.TryGetValue(asset, out var previousTcs))
                previousTcs.TrySetResult(true);

            var cancellationTcs = new TaskCompletionSource<bool>();
            k_AssetCancellationDict[asset] = cancellationTcs;

            try
            {
                var workTask = WaitForCloudProjectSettings();
                var completedTask = await Task.WhenAny(workTask, cancellationTcs.Task);

                if (completedTask == cancellationTcs.Task)
                    return false;

                await workTask;
                return true;
            }
            finally
            {
                if (k_AssetCancellationDict.TryGetValue(asset, out var currentTcs) && currentTcs == cancellationTcs)
                    k_AssetCancellationDict.Remove(asset);
            }
        }

        public static async Task<bool> WaitForCloudProjectSettings(CancellationToken cancellationToken = default)
        {
            while (AreCloudProjectSettingsInvalid())
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                await Task.Yield();
            }

            return true;
        }

        public static bool AreCloudProjectSettingsInvalid() => string.IsNullOrWhiteSpace(CloudProjectSettings.organizationKey) || string.IsNullOrWhiteSpace(CloudProjectSettings.userId);
        public static bool AreCloudProjectSettingsValid() => !AreCloudProjectSettingsInvalid();

        const string k_InternalMenu = "internal:";
        const string k_SimulateClientSideFailuresMenu = "AI Toolkit/Internals/Tests/Simulate Client Side Failures";
        const string k_SimulateClientSideFailuresKey = "AI_Toolkit_Simulate_Client_Side_Failures";

        public static bool simulateClientSideFailures
        {
            get => EditorPrefs.GetBool(k_SimulateClientSideFailuresKey, false);
            set => EditorPrefs.SetBool(k_SimulateClientSideFailuresKey, value);
        }

        [MenuItem(k_InternalMenu + k_SimulateClientSideFailuresMenu, false, 101)]
        static void ToggleSimulateClientSideFailures()
        {
            simulateClientSideFailures = !simulateClientSideFailures;
        }
        [MenuItem(k_InternalMenu + k_SimulateClientSideFailuresMenu, true, 101)]
        static bool ValidateSimulateClientSideFailures()
        {
            Menu.SetChecked(k_SimulateClientSideFailuresMenu, simulateClientSideFailures);
            return true;
        }

        const string k_SimulateServerSideFailuresMenu = "AI Toolkit/Internals/Tests/Simulate Server Side Failures";
        const string k_SimulateServerSideFailuresKey = "AI_Toolkit_Simulate_Server_Side_Failures";

        public static bool simulateServerSideFailures
        {
            get => EditorPrefs.GetBool(k_SimulateServerSideFailuresKey, false);
            set => EditorPrefs.SetBool(k_SimulateServerSideFailuresKey, value);
        }

        [MenuItem(k_InternalMenu + k_SimulateServerSideFailuresMenu, false, 101)]
        static void ToggleSimulateServerSideFailures()
        {
            simulateServerSideFailures = !simulateServerSideFailures;
        }
        [MenuItem(k_InternalMenu + k_SimulateServerSideFailuresMenu, true, 101)]
        static bool ValidateSimulateServerSideFailures()
        {
            Menu.SetChecked(k_SimulateServerSideFailuresMenu, simulateServerSideFailures);
            return true;
        }
    }
}
