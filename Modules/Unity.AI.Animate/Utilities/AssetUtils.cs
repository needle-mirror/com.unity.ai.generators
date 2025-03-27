using System;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Animate.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Animation";
        public const string defaultAssetExtension = ".anim";
        public const string fbxAssetExtension = ".fbx";

        public static string CreateBlankAnimation(string path, bool force = true)
        {
            var clip = new AnimationClip
            {
                legacy = false,
                wrapMode = WrapMode.Loop
            };

            var zeroCurve = new AnimationCurve(new Keyframe(0f, 0f));
            clip.SetCurve("", typeof(Animator), "RootT.x", zeroCurve);
            clip.SetCurve("", typeof(Animator), "RootT.y", zeroCurve);
            clip.SetCurve("", typeof(Animator), "RootT.z", zeroCurve);

            var zeroRotCurve = new AnimationCurve(new Keyframe(0f, 0f));
            clip.SetCurve("", typeof(Animator), "RootQ.x", zeroRotCurve);
            clip.SetCurve("", typeof(Animator), "RootQ.y", zeroRotCurve);
            clip.SetCurve("", typeof(Animator), "RootQ.z", zeroRotCurve);

            var oneRotCurve = new AnimationCurve(new Keyframe(0f, 1f));
            clip.SetCurve("", typeof(Animator), "RootQ.w", oneRotCurve);

            var muscleNames = HumanTrait.MuscleName;
            foreach (var muscleName in muscleNames)
            {
                var curve = new AnimationCurve(new Keyframe(0f, 0f));
                clip.SetCurve("", typeof(Animator), muscleName, curve);
            }

            clip.EnsureQuaternionContinuity();

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(clip, assetPath);

            clip.SetDefaultClipSettings();

            return assetPath;
        }

        public static void SetDefaultClipSettings(this AnimationClip clip)
        {
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            clipSettings.loopBlendPositionY = true;
            clipSettings.loopBlendPositionXZ = true;
            clipSettings.loopBlendOrientation = true;
            clipSettings.keepOriginalPositionY = true;
            clipSettings.keepOriginalPositionXZ = true;
            clipSettings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static AnimationClip CreateAndSelectBlankAnimation(bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{defaultNewAssetName}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankAnimation(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create animate for '{path}'.");
                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
            }
            var animate = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Selection.activeObject = animate;
            return animate;
        }
    }
}
