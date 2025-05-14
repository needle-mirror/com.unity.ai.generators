﻿using System;
using System.IO;
using Unity.AI.Generators.Asset;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Audio Clip";
        public const string defaultAssetExtension = ".wav";

        public static string CreateBlankAudioClip(string path, bool force = true)
        {
            path = Path.ChangeExtension(path, defaultAssetExtension);
            if (force || !File.Exists(path))
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                {
                    // Use direct-to-file stream approach instead of intermediate byte array
                    using var fileStream = FileIO.OpenWrite(path);
                    AudioClipExtensions.CreateSilentAudioWavFile(fileStream);
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            return path;
        }

        public static AudioClip CreateBlankAudioClipSameFolder(AssetReference assetReference, string nameSuffix = "", bool force = true)
        {
            var assetPath = assetReference.GetPath();

            if (!assetReference.IsValid() || string.IsNullOrEmpty(assetPath))
                return null;

            var basePath = string.Empty;

            if (File.Exists(assetPath))
                basePath = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrEmpty(basePath))
                basePath = "Assets";

            var assetName = Path.GetFileNameWithoutExtension(assetReference.GetPath());

            var path = $"{basePath}/{assetName}{nameSuffix}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankAudioClip(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create audio clip for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return audioClip;
        }

        public static AudioClip CreateAndSelectBlankAudioClip(bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{defaultNewAssetName}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankAudioClip(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create audio clip for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Selection.activeObject = audioClip;
            return audioClip;
        }
    }
}
