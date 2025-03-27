using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using Unity.AI.Generators.Asset;

namespace Unity.AI.Generators.Sdk
{
    class TraceIdProvider : ITraceIdProvider
    {
        readonly AssetReference m_AssetReference;

        public TraceIdProvider(AssetReference asset) => m_AssetReference = asset;

        public Task<string> GetTraceId() => Task.FromResult(m_AssetReference.guid);
    }
}
