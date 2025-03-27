using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Sdk
{
    class Logger : AiEditorToolsSdk.Domain.Abstractions.Services.ILogger
    {
        public void LogDebug(string message)
        {
            if (LoggerUtilities.sdkLogLevel == 0)
                return;
            Debug.Log(message);
        }

        public void LogDebug(Exception exception, string message)
        {
            if (LoggerUtilities.sdkLogLevel == 0)
                return;
            Debug.Log(message);
            LoggerUtilities.LogExceptionAsLog(exception);
        }
        public void LogDebug(Exception exception)
        {
            if (LoggerUtilities.sdkLogLevel == 0)
                return;
            LoggerUtilities.LogExceptionAsLog(exception);
        }

        public void LogPublicInformation(string message) => Debug.Log(message);

        public void LogPublicError(string message) => Debug.LogError(message);
    }

    static class LoggerUtilities
    {
        const string k_SdkLogLevelMenu = "AI Toolkit/Internals/Log All Sdk Messages";
        const string k_SdkLogLevelKey = "AI_Toolkit_Sdk_Log_Level";

        public static int sdkLogLevel
        {
            get => EditorPrefs.GetInt(k_SdkLogLevelKey, 0);
            private set => EditorPrefs.SetInt(k_SdkLogLevelKey, value);
        }

        [MenuItem(k_SdkLogLevelMenu, false, 1020)]
        static void ToggleSdkLogLevel()
        {
            sdkLogLevel = sdkLogLevel == 1 ? 0 : 1;
        }
        [MenuItem(k_SdkLogLevelMenu, true, 1020)]
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
