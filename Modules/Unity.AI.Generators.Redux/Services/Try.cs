using System;
using UnityEngine;

namespace Unity.AI.Generators.Redux.Services
{
    static class Try
    {
        public static void Safely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
