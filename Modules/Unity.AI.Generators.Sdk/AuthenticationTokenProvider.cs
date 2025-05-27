using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using AiEditorToolsSdk.Domain.Core.Results;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Sdk
{
    class AuthenticationTokenProvider : IUnityAuthenticationTokenProvider
    {
        readonly int m_MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        string m_Token = CloudProjectSettings.accessToken;

        public async Task<Result<string>> ForceRefreshToken()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
                return await GetToken();

            var tcs = new TaskCompletionSource<bool>();
            CloudProjectSettings.RefreshAccessToken(callbackStatus => tcs.TrySetResult(callbackStatus));

            const int timeoutSeconds = 10;
            var completedTask = await Task.WhenAny(tcs.Task, EditorTask.Delay((int)TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds));
            if (completedTask == tcs.Task)
            {
                var status = await tcs.Task;
                if (status)
                {
                    m_Token = CloudProjectSettings.accessToken;
                    Debug.Log("Access token refreshed successfully.");
                    return Result<string>.Ok(m_Token);
                }

                Debug.LogError($"Token refresh failed or was not needed.");
                return Result<string>.Ok(m_Token);
            }

            Debug.LogWarning($"Token refresh timed out after {timeoutSeconds} seconds.");
            return Result<string>.Fail();
        }

        public Task<Result<string>> GetToken() => Task.FromResult(System.Threading.Thread.CurrentThread.ManagedThreadId != m_MainThreadId
            ? Result<string>.Ok(m_Token)
            : Result<string>.Ok(m_Token = CloudProjectSettings.accessToken));
    }
}
