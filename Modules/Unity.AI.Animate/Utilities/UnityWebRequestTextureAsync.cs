using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AI.Animate.Services.Utilities
{
    static class UnityWebRequestTextureAsync
    {
        public static async Task<Texture> GetContent(string url)
        {
            using var request = UnityWebRequestTexture.GetTexture(url);
            var task = request.SendWebRequest();
            await task;
            if (task.webRequest.result != UnityWebRequest.Result.Success)
                return null;
            var result = DownloadHandlerTexture.GetContent(request);
            result.hideFlags = HideFlags.HideAndDontSave;
            return result;
        }
    }
}
