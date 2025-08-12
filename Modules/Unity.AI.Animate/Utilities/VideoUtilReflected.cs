using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.AI.Animate.Services.Utilities
{
    /// <summary>
    /// A reflection-based wrapper for the internal UnityEditor.VideoUtil class.
    /// This allows accessing the editor's video preview system from any editor script.
    /// The preview system is capable of playing video even when the editor is paused.
    /// </summary>
    static class VideoUtilReflected
    {
        static Type s_VideoUtilType;
        static MethodInfo s_StartPreviewMethod;
        static MethodInfo s_StopPreviewMethod;
        static MethodInfo s_PlayPreviewMethod;
        static MethodInfo s_PausePreviewMethod;
        static MethodInfo s_GetPreviewTextureMethod;

        static bool s_IsInitialized = false;

        static VideoUtilReflected() => Initialize();

        static void Initialize()
        {
            if (s_IsInitialized)
                return;

            // The most direct way when the assembly name is known.
            // Format: "Namespace.TypeName, AssemblyName"
            s_VideoUtilType = Type.GetType("UnityEditor.VideoUtil, UnityEditor.VideoModule");

            if (s_VideoUtilType == null)
            {
                Debug.LogError("VideoUtilReflected: Could not find internal class UnityEditor.VideoUtil in assembly UnityEditor.VideoModule. This may be due to a Unity version change or the module not being loaded.");
                return;
            }

            // Get MethodInfo for each static method we want to call.
            // We need to specify the parameter types to correctly resolve overloads.
            s_StartPreviewMethod = s_VideoUtilType.GetMethod("StartPreview", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(VideoClip) }, null);
            s_StopPreviewMethod = s_VideoUtilType.GetMethod("StopPreview", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(GUID) }, null);
            s_PlayPreviewMethod = s_VideoUtilType.GetMethod("PlayPreview", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(GUID), typeof(bool) }, null);
            s_PausePreviewMethod = s_VideoUtilType.GetMethod("PausePreview", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(GUID) }, null);
            s_GetPreviewTextureMethod = s_VideoUtilType.GetMethod("GetPreviewTexture", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(GUID) }, null);

            // Verify that all methods were found.
            if (s_StartPreviewMethod == null || s_StopPreviewMethod == null || s_PlayPreviewMethod == null || s_PausePreviewMethod == null || s_GetPreviewTextureMethod == null)
            {
                Debug.LogError("VideoUtilReflected: Could not find one or more methods on UnityEditor.VideoUtil. This may be due to a Unity version change.");
                s_VideoUtilType = null; // Invalidate the type if we failed.
                return;
            }

            s_IsInitialized = true;
        }

        /// <summary>
        /// Starts a video preview and returns a GUID to identify it.
        /// </summary>
        /// <param name="clip">The VideoClip to preview.</param>
        /// <returns>A GUID handle for the preview instance.</returns>
        public static GUID StartPreview(VideoClip clip)
        {
            if (!s_IsInitialized) return new GUID();
            // Invoke the static method. 'null' for the first parameter means it's a static call.
            var result = s_StartPreviewMethod.Invoke(null, new object[] { clip });
            return (GUID)result;
        }

        /// <summary>
        /// Stops a video preview and releases its resources.
        /// </summary>
        /// <param name="previewID">The GUID of the preview to stop.</param>
        public static void StopPreview(GUID previewID)
        {
            if (!s_IsInitialized || previewID.Empty()) return;
            s_StopPreviewMethod.Invoke(null, new object[] { previewID });
        }

        /// <summary>
        /// Starts or resumes playback of a preview instance.
        /// </summary>
        /// <param name="previewID">The GUID of the preview to play.</param>
        /// <param name="loop">Whether the video should loop.</param>
        public static void PlayPreview(GUID previewID, bool loop)
        {
            if (!s_IsInitialized || previewID.Empty()) return;
            s_PlayPreviewMethod.Invoke(null, new object[] { previewID, loop });
        }

        /// <summary>
        /// Pauses playback of a preview instance.
        /// </summary>
        /// <param name="previewID">The GUID of the preview to pause.</param>
        public static void PausePreview(GUID previewID)
        {
            if (!s_IsInitialized || previewID.Empty()) return;
            s_PausePreviewMethod.Invoke(null, new object[] { previewID });
        }

        /// <summary>
        /// Gets the current texture for a preview instance.
        /// </summary>
        /// <param name="previewID">The GUID of the preview.</param>
        /// <returns>The Texture containing the current video frame, or null.</returns>
        public static Texture GetPreviewTexture(GUID previewID)
        {
            if (!s_IsInitialized || previewID.Empty()) return null;
            var result = s_GetPreviewTextureMethod.Invoke(null, new object[] { previewID });
            return (Texture)result;
        }
    }
}
