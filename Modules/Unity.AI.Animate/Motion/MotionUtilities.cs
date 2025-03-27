﻿using System;
using UnityEngine;

namespace Unity.AI.Animate.Motion
{
    static class MotionUtilities
    {
        // ----------------
        // Simple base64 decode (inline, no pooled arrays)
        // ----------------
        public static float[] DecodeFloatsFromBase64(string base64, int floatsPerElement, out int elemCount)
        {
            if (string.IsNullOrEmpty(base64))
            {
                elemCount = 0;
                return Array.Empty<float>();
            }

            var bytes = Convert.FromBase64String(base64);
            var totalFloats = bytes.Length / 4;
            elemCount = totalFloats / floatsPerElement;

            // Convert the raw bytes to single-precision floats
            var floats = new float[totalFloats];
            for (var i = 0; i < totalFloats; i++)
                floats[i] = BitConverter.ToSingle(bytes, i * 4);

            return floats;
        }

        // ----------------
        // Utility structs for building root positions/rotations
        // ----------------

        public struct PositionCurve
        {
            public AnimationCurve x, y, z;
            public static PositionCurve New() => new() { x = new(), y = new(), z = new() };
            public void AddKey(float time, Vector3 position)
            {
                x.AddKey(time, position.x);
                y.AddKey(time, position.y);
                z.AddKey(time, position.z);
            }
        }

        public struct RotationCurve
        {
            public AnimationCurve x, y, z, w;
            public static RotationCurve New() => new() { x = new(), y = new(), z = new(), w = new() };
            public void AddKey(float time, Quaternion rotation)
            {
                x.AddKey(time, rotation.x);
                y.AddKey(time, rotation.y);
                z.AddKey(time, rotation.z);
                w.AddKey(time, rotation.w);
            }
        }

        /// <summary>
        /// Helper to set the root transforms and muscle curves on an AnimationClip,
        /// then fix quaternion continuity.
        /// </summary>
        public static void SetHumanoidCurves(
            this AnimationClip clip,
            PositionCurve rootPos,
            RotationCurve rootRot,
            AnimationCurve[] muscleCurves)
        {
            clip.ClearCurves();

            // Root position
            clip.SetCurve("", typeof(Animator), "RootT.x", rootPos.x);
            clip.SetCurve("", typeof(Animator), "RootT.y", rootPos.y);
            clip.SetCurve("", typeof(Animator), "RootT.z", rootPos.z);

            // Root rotation
            clip.SetCurve("", typeof(Animator), "RootQ.x", rootRot.x);
            clip.SetCurve("", typeof(Animator), "RootQ.y", rootRot.y);
            clip.SetCurve("", typeof(Animator), "RootQ.z", rootRot.z);
            clip.SetCurve("", typeof(Animator), "RootQ.w", rootRot.w);

            // Muscles
            for (var i = 0; i < muscleCurves.Length; i++)
            {
                // The HumanTrait.MuscleName array or a similar approach
                // can be used for correct muscle property names
                var muscleName = HumanTrait.MuscleName[i];
                clip.SetCurve("", typeof(Animator), muscleName, muscleCurves[i]);
            }

            // Ensure we fix quaternion boundary issues
            clip.EnsureQuaternionContinuity();
        }
    }
}
