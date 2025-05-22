using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using AiEditorToolsSdk.Domain.Core.Results;
using UnityEditor;

namespace Unity.AI.Generators.Sdk
{
    class AuthenticationTokenProvider : IUnityAuthenticationTokenProvider
    {
        readonly int m_MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        string m_Token = CloudProjectSettings.accessToken;

        public Task<Result<string>> ForceRefreshToken() => GetToken();

        public Task<Result<string>> GetToken() => Task.FromResult(System.Threading.Thread.CurrentThread.ManagedThreadId != m_MainThreadId
            ? Result<string>.Ok(m_Token)
            : Result<string>.Ok(m_Token = CloudProjectSettings.accessToken));
    }
}
