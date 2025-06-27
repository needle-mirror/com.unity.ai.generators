using System.Collections.Generic;

namespace Unity.AI.Generators.UI.Utilities
{
    static class AudioFileUtilities
    {
        public static bool TryGetAudioClipExtension(IReadOnlyList<byte> data, out string extension)
        {
            extension = null;

            if (data == null || data.Count < 4)
                return false;

            if (FileIO.IsWav(data))
            {
                extension = ".wav";
                return true;
            }

            return false; // Unsupported image type
        }
    }
}
