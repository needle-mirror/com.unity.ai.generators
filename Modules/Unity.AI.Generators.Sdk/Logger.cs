using System;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Sdk
{
    class Logger : AiEditorToolsSdk.Domain.Abstractions.Services.ILogger
    {
        public void LogDebug(string message)
        {
            try
            {
                if (LoggerUtilities.sdkLogLevel == 0)
                    return;

                EditorTask.RunOnMainThread(() =>
                {
                    Debug.Log(message);
                });
            }
            catch
            {
                // Silent catch with no logging
            }
        }

        public void LogDebug(Exception exception, string message)
        {
            try
            {
                if (LoggerUtilities.sdkLogLevel == 0)
                    return;

                EditorTask.RunOnMainThread(() =>
                {
                    Debug.Log(message);
                    LoggerUtilities.LogExceptionAsLog(exception);
                });
            }
            catch
            {
                // Silent catch with no logging
            }
        }
        
        public void LogDebug(Exception exception)
        {
            try
            {
                if (LoggerUtilities.sdkLogLevel == 0)
                    return;

                EditorTask.RunOnMainThread(() =>
                {
                    LoggerUtilities.LogExceptionAsLog(exception);
                });
            }
            catch
            {
                // Silent catch with no logging
            }
        }

        public void LogPublicInformation(string message)
        {
            try
            {
                EditorTask.RunOnMainThread(() =>
                {
                    Debug.Log(message);
                });
            }
            catch
            {
                // Silent catch with no logging
            }
        }

        public void LogPublicError(string message)
        {
            try
            {
                EditorTask.RunOnMainThread(() =>
                {
                    Debug.LogError(message);
                });
            }
            catch
            {
                // Silent catch with no logging
            }
        }
    }

    static class LoggerUtilities
    {
        const string k_InternalMenu = "internal:";
        const string k_SdkLogLevelMenu = "AI Toolkit/Internals/Log All Sdk Messages";
        const string k_SdkLogLevelKey = "AI_Toolkit_Sdk_Log_Level";

        public static int sdkLogLevel
        {
            get => EditorPrefs.GetInt(k_SdkLogLevelKey, 0);
            private set => EditorPrefs.SetInt(k_SdkLogLevelKey, value);
        }

        [MenuItem(k_InternalMenu + k_SdkLogLevelMenu, false, 1020)]
        static void ToggleSdkLogLevel()
        {
            sdkLogLevel = sdkLogLevel == 1 ? 0 : 1;
        }
        [MenuItem(k_InternalMenu + k_SdkLogLevelMenu, true, 1020)]
        static bool ValidateSdkLogLevel()
        {
            Menu.SetChecked(k_SdkLogLevelMenu, sdkLogLevel == 1);
            return true;
        }

        /// <summary>
        /// Logs an exception as a normal log while preserving the stack trace format
        /// similar to Debug.LogException
        /// </summary>
        public static void LogExceptionAsLog(Exception exception, UnityEngine.Object context = null)
        {
            var oldHandler = Debug.unityLogger.logHandler;
            var customHandler = new ExceptionAsInfoLogHandler(oldHandler);
            Debug.unityLogger.logHandler = customHandler;

            try
            {
                Debug.LogException(exception, context);
            }
            finally
            {
                Debug.unityLogger.logHandler = oldHandler;
            }
        }
    }

    class ExceptionAsInfoLogHandler : ILogHandler
    {
        readonly ILogHandler m_OriginalHandler;

        public ExceptionAsInfoLogHandler(ILogHandler originalHandler) => m_OriginalHandler = originalHandler;

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) =>
            m_OriginalHandler.LogFormat(logType is LogType.Exception or LogType.Error ? LogType.Log : logType, context, format, args);

        public void LogException(Exception exception, UnityEngine.Object context) =>
            m_OriginalHandler.LogFormat(LogType.Log, context, "{0}", exception.ToString());
    }
}
