#if UNITY_AI_GENERATED_ASSETS_FOLDER_FIX
using System;
using System.IO;
using UnityEngine;

namespace Unity.AI.Generators.Asset
{
    static class FolderIO
    {
        public static void TryMergeAndAlwaysDeleteFolder(string sourceFolderPath, string destinationFolderPath, bool overwriteFiles = true)
        {
            if (string.IsNullOrWhiteSpace(sourceFolderPath) || string.IsNullOrWhiteSpace(destinationFolderPath))
            {
                Debug.Log($"Source or destination path is empty. Operation skipped.");
                return;
            }

            try
            {
                sourceFolderPath = Path.GetFullPath(sourceFolderPath);
                destinationFolderPath = Path.GetFullPath(destinationFolderPath);
            }
            catch (Exception ex)
            {
                Debug.Log($"Path normalization failed: {ex.Message}");
            }

            if (string.Equals(sourceFolderPath, destinationFolderPath, StringComparison.OrdinalIgnoreCase))
                return;

            // Source folder not found
            if (!Directory.Exists(sourceFolderPath))
                return;

            try
            {
                if (!Directory.Exists(destinationFolderPath))
                {
                    try
                    {
                        Directory.Move(sourceFolderPath, destinationFolderPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Failed to move folder, falling back to copy and delete: {ex.Message}");
                        TryCopyFolderContents(sourceFolderPath, destinationFolderPath, overwriteFiles);
                    }
                }
                else
                {
                    if (File.Exists(destinationFolderPath))
                    {
                        Debug.Log($"Destination exists as a file, not a folder. Creating alternative destination.");
                        destinationFolderPath += "_old";
                        if (!Directory.Exists(destinationFolderPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(destinationFolderPath);
                            }
                            catch (Exception ex)
                            {
                                Debug.Log($"Failed to create destination folder: {ex.Message}");
                            }
                        }
                    }

                    TryCopyFolderContents(sourceFolderPath, destinationFolderPath, overwriteFiles);
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error during folder operation: {ex.Message}");
            }
            finally
            {
                TryDeleteFolder(sourceFolderPath);
            }
        }

        static void TryCopyFolderContents(string sourceDir, string destDir, bool overwrite)
        {
            if (!Directory.Exists(destDir))
            {
                try
                {
                    Directory.CreateDirectory(destDir);
                }
                catch (Exception ex)
                {
                    Debug.Log($"Failed to create directory {destDir}: {ex.Message}");
                    return;
                }
            }

            foreach (var sourceFile in TryGetFiles(sourceDir))
            {
                try
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var destFile = Path.Combine(destDir, fileName);

                    if (File.Exists(destFile))
                    {
                        if (overwrite)
                        {
                            try
                            {
                                File.Delete(destFile);
                            }
                            catch (Exception ex)
                            {
                                Debug.Log($"Failed to delete existing file {destFile}: {ex.Message}");
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }

                    try
                    {
                        File.Copy(sourceFile, destFile, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Failed to copy file from {sourceFile} to {destFile}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error processing file {sourceFile}: {ex.Message}");
                }
            }

            foreach (var sourceSubDir in TryGetDirectories(sourceDir))
            {
                try
                {
                    var dirName = Path.GetFileName(sourceSubDir);
                    var destSubDir = Path.Combine(destDir, dirName);

                    TryCopyFolderContents(sourceSubDir, destSubDir, overwrite);
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error processing directory {sourceSubDir}: {ex.Message}");
                }
            }
        }

        static string[] TryGetFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to get files from {path}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        static string[] TryGetDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to get directories from {path}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        static void TryDeleteFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to delete folder {folderPath}: {ex.Message}");

                try
                {
                    foreach (var file in TryGetFiles(folderPath))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* ignored */ }
                    }

                    foreach (var dir in TryGetDirectories(folderPath))
                    {
                        TryDeleteFolder(dir);
                    }

                    if (Directory.Exists(folderPath))
                    {
                        Directory.Delete(folderPath);
                    }
                }
                catch { /* ignored */ }
            }
        }
    }
}
#endif
