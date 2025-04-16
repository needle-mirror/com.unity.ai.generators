using System;
using System.IO;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class FileUtilities
    {
        public const string failedDownloadPath = "Packages/com.unity.ai.generators/Modules/Unity.AI.Image/Images/FailedDownload.png";

        public static string GetFailedImageUrl(string guid)
        {
            var sourceFile = Path.GetFullPath(failedDownloadPath);
            var tempFolder = Path.Combine(UndoUtilities.projectRootPath, "Temp");

            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            var destinationFile = Path.Combine(tempFolder, guid);
            destinationFile = Path.ChangeExtension(destinationFile, Path.GetExtension(sourceFile));
            File.Copy(sourceFile, destinationFile, true);

            var fileUri = new Uri(destinationFile);
            return fileUri.AbsoluteUri;
        }
    }
}
