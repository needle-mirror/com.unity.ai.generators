using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using AiEditorToolsSdk.Domain.Core.Results;
using UnityEditor;

namespace Unity.AI.Generators.Sdk
{
    class AuthenticationTokenProvider : IUnityAuthenticationTokenProvider
    {
        public Task<Result<string>> ForceRefreshToken() => GetToken();

        public Task<Result<string>> GetToken() => Task.FromResult(Result<string>.Ok(CloudProjectSettings.accessToken));
    }
}
