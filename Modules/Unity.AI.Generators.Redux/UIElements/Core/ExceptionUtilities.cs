using System;
using System.Diagnostics;
using UnityEditor;

namespace Unity.AI.Generators.UIElements.Core
{
    static class ExceptionUtilities
    {
        public static Exception AggregateStack(Exception exception, StackTrace inner)
        {
            var fullException = exception;
            if (inner != null)
                fullException = new AggregateException(
                    exception,
                    new Exception("\n\n********************************* Source Stack Trace ******************************************\n" + inner + "\n************************************************************************************\n\n"));
            else
                detailedExceptionStack = true;
            return fullException;
        }

        const string k_InternalMenu = "internal:";
        const string k_DetailedExceptionStackMenu = "AI Toolkit/Internals/Detailed Redux Exceptions";
        public static bool detailedExceptionStack;

        [MenuItem(k_InternalMenu + k_DetailedExceptionStackMenu, false, 1021)]
        static void ToggleDetailedException() => detailedExceptionStack = !detailedExceptionStack;

        [MenuItem(k_InternalMenu + k_DetailedExceptionStackMenu, true, 1021)]
        static bool ValidateDetailedException()
        {
            Menu.SetChecked(k_DetailedExceptionStackMenu, detailedExceptionStack);
            return true;
        }
    }
}
