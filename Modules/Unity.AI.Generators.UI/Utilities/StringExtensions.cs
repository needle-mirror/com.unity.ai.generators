using System.Text.RegularExpressions;

namespace Unity.AI.Generators.UI.Utilities
{
    static class StringExtensions
    {
        public static string AddSpaceBeforeCapitalLetters(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Use Regex to replace any capital letter with a space followed by the capital letter
            // "(?<!^)" ensures we don't add a space at the start of the string
            return Regex.Replace(input, "(?<!^)([A-Z])", " $1");
        }
    }
}
