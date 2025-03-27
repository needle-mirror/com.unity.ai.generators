using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class ProgressUtils
    {
        internal static async Task RunFuzzyProgress(float startValue, float endValue, Action<float> onStep, int workSize, CancellationToken token, int intervalMs = 50)
        {
            var value = startValue;
            var rate = 0.33f / (intervalMs * Mathf.Sqrt(workSize));

            while (!token.IsCancellationRequested)
            {
                value += (endValue - value) * rate;
                onStep(value);
                await Task.Delay(intervalMs, token);
            }
        }
    }
}
