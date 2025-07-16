using System;
using System.IO;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class UndoUtilities
    {
        public static readonly string projectRootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string GetTempFileName()
        {
            // this folder is automatically cleaned up by Unity Editor
            var tempFolderPath = Path.Combine(projectRootPath, "Temp", "Generated Assets", "Undo");

            if (!Directory.Exists(tempFolderPath))
                Directory.CreateDirectory(tempFolderPath);

            var fileName = Guid.NewGuid().ToString("N") + ".tmp";
            var fullFilePath = Path.Combine(tempFolderPath, fileName);

            using (File.Create(fullFilePath))
            {
                // release immediately
            }

            return fullFilePath;
        }
    }
}
