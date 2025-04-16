using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Utilities
{
    static class WebUtils
    {
        public const string prodEnvironment = "https://musetools.unity.com";
        public const string stagingEnvironment = "https://musetools-stg.unity.com";
        public const string testEnvironment = "https://musetools-test.unity.com";
        public const string localEnvironment = "https://localhost:5050";

        internal static string selectedEnvironment { get; set; } = stagingEnvironment;
    }
}
