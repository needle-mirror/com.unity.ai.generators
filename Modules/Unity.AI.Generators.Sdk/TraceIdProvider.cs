using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using Unity.AI.Generators.Asset;
using Unity.AI.Toolkit;
using UnityEditor;

namespace Unity.AI.Generators.Sdk
{
    class TraceIdProvider : ITraceIdProvider
    {
        readonly AssetReference m_AssetReference;

        public TraceIdProvider(AssetReference asset) => m_AssetReference = asset;

        public async Task<string> GetTraceId()
        {
            var id = await EditorTask.RunOnMainThread(() => Task.FromResult(EditorAnalyticsSessionInfo.id));
            return $"{m_AssetReference.guid}&{id}";
        }
    }
}
