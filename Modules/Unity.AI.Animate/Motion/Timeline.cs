using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.AI.Animate.Motion
{
    /// <summary>
    /// Manages a series of "PoseModels"—one for each frame—and can bake from a VideoToMotionResponse.
    /// Then, optionally, you can export them or apply them to an Animator, etc.
    /// </summary>
    class Timeline
    {
        PoseModel[] m_Poses;

        const float k_OutputFps = 30f;

        /// <summary>
        /// Bake frames from a Response into this timeline, capturing local transforms
        /// and applying the response data to each frame's PoseModel.
        /// Optionally resamples if input FPS != 30.
        /// </summary>
        public bool BakeFromResponse(
            IReadOnlyList<MotionResponse.Frame> responseFrames,
            float responseFps,
            ArmatureMapping armature)
        {
            if (responseFrames == null || responseFrames.Count == 0 || armature == null)
                return false;

            // Allocate a PoseModel per frame
            m_Poses = new PoseModel[responseFrames.Count];
            var numJoints = armature.joints?.Length ?? 0;
            for (var i = 0; i < m_Poses.Length; i++)
                m_Poses[i] = new PoseModel(numJoints);

            // Fill local transforms from the frame data
            for (var frameIndex = 0; frameIndex < responseFrames.Count; frameIndex++)
            {
                var frame = responseFrames[frameIndex]; // positions and rotations
                // We first capture the local transforms from the *current* scene state
                // (assuming the armature is in some default T-pose).
                // Then we overwrite the joints to match the new root position + rotation array.

                var pose = m_Poses[frameIndex];
                pose.CaptureLocal(armature);

                // Root is joint 0. If positions[] has at least 1 element, set that as the root pos
                if (frame.positions.Length > 0 && frameIndex < m_Poses.Length)
                {
                    var rootPos = frame.positions[0];
                    // Overwrite the "pos" in the local array
                    var rt = pose.local[0];
                    pose.local[0] = new RigidTransform(rt.rot, rootPos);
                }

                // If rotations[] matches the number of joints, apply them all
                // (Alternatively, you might have a mapping approach if your new skeleton
                //  doesn't line up 1:1 with the frame data.)
                for (var j = 0; j < numJoints && j < frame.rotations.Length; j++)
                {
                    var localRT = pose.local[j];
                    pose.local[j] = new RigidTransform(frame.rotations[j], localRT.pos);
                }

                m_Poses[frameIndex] = pose;
            }

            // If responseFps != 30, resample to 30
            if (Mathf.Abs(responseFps - k_OutputFps) > 0.01f)
                ResampleInPlace(responseFps, k_OutputFps);

            return true;
        }

        /// <summary>
        /// Resample the local poses in this timeline to a new framerate, overwriting m_Poses.
        /// Then re-apply them to the given ArmatureMapping so that further steps can capture or export if desired.
        /// </summary>
        void ResampleInPlace(float oldFps, float newFps)
        {
            if (m_Poses == null || m_Poses.Length == 0)
                return;

            var totalTime = m_Poses.Length / oldFps;
            var newCount = Mathf.RoundToInt(totalTime * newFps);
            if (newCount <= 0)
                newCount = 1;

            var oldPoses = m_Poses;
            m_Poses = new PoseModel[newCount];

            var numJoints = oldPoses[0].local.Length;
            for (var i = 0; i < newCount; i++)
                m_Poses[i] = new PoseModel(numJoints);

            for (var i = 0; i < newCount; i++)
            {
                var t = i / newFps;
                var oldIndexFloat = t * oldFps;
                var idx0 = Mathf.FloorToInt(oldIndexFloat);
                var frac = oldIndexFloat - idx0;
                var idx1 = idx0 + 1;
                if (idx0 >= oldPoses.Length)
                    idx0 = oldPoses.Length - 1;
                if (idx1 >= oldPoses.Length)
                    idx1 = oldPoses.Length - 1;

                var poseA = oldPoses[idx0];
                var poseB = oldPoses[idx1];

                m_Poses[i].InterpolateLocal(poseA, poseB, frac);
            }
        }

        /// <summary>
        /// Export the (already baked) timeline to a humanoid AnimationClip
        /// using the provided ArmatureMapping (which must have an Animator & Avatar).
        /// </summary>
        public AnimationClip ExportToHumanoidClip(ArmatureMapping poseArmature)
        {
            if (!poseArmature.TryGetComponent<Animator>(out var animator))
                return null;

            var clip = new AnimationClip { legacy = false };

            if (m_Poses == null || m_Poses.Length == 0)
                return clip;

            using var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var humanPose = new HumanPose();
            handler.GetHumanPose(ref humanPose);

            var rootPos = MotionUtilities.PositionCurve.New();
            var rootRot = MotionUtilities.RotationCurve.New();

            var muscleCurves = new AnimationCurve[humanPose.muscles.Length];
            for (var i = 0; i < muscleCurves.Length; i++)
                muscleCurves[i] = new AnimationCurve();

            for (var f = 0; f < m_Poses.Length; f++)
            {
                var time = f / k_OutputFps;
                m_Poses[f].ApplyLocal(poseArmature, Vector3.zero, Quaternion.identity);
                handler.GetHumanPose(ref humanPose);
                rootPos.AddKey(time, humanPose.bodyPosition);
                rootRot.AddKey(time, humanPose.bodyRotation);
                for (var m = 0; m < humanPose.muscles.Length; m++)
                {
                    muscleCurves[m].AddKey(time, humanPose.muscles[m]);
                }
            }

            clip.SetHumanoidCurves(rootPos, rootRot, muscleCurves);

            return clip;
        }
    }
}
