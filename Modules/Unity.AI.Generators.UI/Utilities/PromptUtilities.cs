namespace Unity.AI.Generators.UI.Utilities
{
    static class PromptUtilities
    {
        public const int maxPromptLength = 1024;
        
        public static string TruncatePrompt(string prompt) => prompt.Length > maxPromptLength ? prompt[..maxPromptLength] : prompt;
    }
}
