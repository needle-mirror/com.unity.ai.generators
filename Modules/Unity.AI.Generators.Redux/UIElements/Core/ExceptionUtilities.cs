using System;
using System.Diagnostics;

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

            return fullException;
        }
    }
}
