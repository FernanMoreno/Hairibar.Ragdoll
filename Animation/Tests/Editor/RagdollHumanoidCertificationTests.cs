using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollHumanoidCertificationTests
    {
        GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance) UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void A02_ImportedAvatar_ResolvesCompleteBipedReferences()
        {
            Animator animator = InstantiateHumanoid();
            RagdollBipedReferences references;
            string error;
            Assert.That(RagdollBipedReferences.TryFromHumanoid(
                animator, out references, out error), Is.True, error);
            Assert.That(references.hips, Is.SameAs(
                animator.GetBoneTransform(HumanBodyBones.Hips)));
            Assert.That(references.leftHand, Is.SameAs(
                animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            Assert.That(references.rightFoot, Is.SameAs(
                animator.GetBoneTransform(HumanBodyBones.RightFoot)));
        }

        [Test]
        public void A12_AlternateHumanoidInstances_ReuseSemanticAvatarContract()
        {
            Animator first = InstantiateHumanoid();
            GameObject secondObject = UnityEngine.Object.Instantiate(
                LoadHumanoidAsset());
            try
            {
                Animator second = secondObject.GetComponentInChildren<Animator>(true);
                Assert.That(first.avatar.isHuman, Is.True);
                Assert.That(second.avatar.isHuman, Is.True);
                Assert.That(first.GetBoneTransform(HumanBodyBones.Hips), Is.Not.Null);
                Assert.That(second.GetBoneTransform(HumanBodyBones.Hips), Is.Not.Null);
                Assert.That(first.GetBoneTransform(HumanBodyBones.Hips),
                    Is.Not.SameAs(second.GetBoneTransform(HumanBodyBones.Hips)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void B22_SharedHumanoidProfile_UsesSemanticsInsteadOfTransformNames()
        {
            Animator animator = InstantiateHumanoid();
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            foreach (HumanBodyBones bone in new[]
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.RightUpperLeg
            })
            {
                Assert.That(animator.GetBoneTransform(bone), Is.Not.Null, bone.ToString());
            }
        }

        [Test]
        public void E01_GeneratedController_ContainsFallStatesAndRuntimeParameter()
        {
            AnimatorController controller = LoadController();
            Assert.That(controller.parameters.Any(value =>
                value.name == "FallBlend"
                && value.type == AnimatorControllerParameterType.Float), Is.True);
            string[] states = controller.layers[0].stateMachine.states
                .Select(value => value.state.name)
                .ToArray();
            CollectionAssert.IsSubsetOf(
                new[] { "Locomotion", "Fall", "GetUp Prone", "GetUp Supine" },
                states);
        }

        [Test]
        public void I02_HumanoidBaker_UsesRealAvatarAndAvatarRoot()
        {
            Animator animator = InstantiateHumanoid();
            RagdollHumanoidBaker baker =
                animator.gameObject.AddComponent<RagdollHumanoidBaker>();
            Assert.That(baker.Animator, Is.SameAs(animator));
            Assert.That(baker.RecordingRoot, Is.SameAs(animator.transform));
            using (HumanPoseHandler handler = new HumanPoseHandler(
                animator.avatar,
                animator.avatarRoot))
            {
                HumanPose pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Assert.That(pose.muscles.Length,
                    Is.EqualTo(HumanTrait.MuscleCount));
            }
        }

        [Test]
        public void I08_HumanoidRecorder_WritesFeetAndHandIkCurves()
        {
            Animator animator = InstantiateHumanoid();
            RagdollHumanoidBaker baker =
                animator.gameObject.AddComponent<RagdollHumanoidBaker>();
            baker.bakeHandIK = true;
            Type recorderType = typeof(RagdollBakerSessionManager).GetNestedType(
                "HumanoidClipRecorder",
                BindingFlags.NonPublic);
            Assert.That(recorderType, Is.Not.Null);
            object recorder = Activator.CreateInstance(
                recorderType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { baker },
                null);
            AnimationClip clip = new AnimationClip();
            try
            {
                recorderType.GetMethod("Sample").Invoke(recorder, new object[] { 0f });
                recorderType.GetMethod("Sample").Invoke(recorder, new object[] { 1f / 60f });
                recorderType.GetMethod("Save").Invoke(recorder, new object[] { clip });
                string[] properties = AnimationUtility.GetCurveBindings(clip)
                    .Select(value => value.propertyName)
                    .ToArray();
                Assert.That(properties, Does.Contain("LeftFootT.x"));
                Assert.That(properties, Does.Contain("RightFootQ.w"));
                Assert.That(properties, Does.Contain("LeftHandT.x"));
                Assert.That(properties, Does.Contain("RightHandQ.w"));
            }
            finally
            {
                ((IDisposable)recorder).Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void I09_GeneratedController_HasTwoIkPassLayersAndRealClips()
        {
            AnimatorController controller = LoadController();
            Assert.That(controller.layers.Length, Is.EqualTo(2));
            Assert.That(controller.layers[0].iKPass, Is.True);
            Assert.That(controller.layers[1].iKPass, Is.True);
            Assert.That(controller.layers[0].stateMachine.defaultState.motion,
                Is.TypeOf<AnimationClip>());
            Assert.That(controller.layers[1].stateMachine.defaultState.motion,
                Is.TypeOf<AnimationClip>());
        }

        Animator InstantiateHumanoid()
        {
            instance = UnityEngine.Object.Instantiate(LoadHumanoidAsset());
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            animator.runtimeAnimatorController = LoadController();
            return animator;
        }

        static GameObject LoadHumanoidAsset()
        {
            string[] guids = AssetDatabase.FindAssets(
                "FBX_MixamoBot t:GameObject",
                new[] { "Assets/Samples/Hairibar.Ragdoll/2.0.0/Demo Scenes" });
            Assert.That(guids.Length, Is.EqualTo(1),
                "Run HairibarCertification.PrepareAssets before certification tests.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static AnimatorController LoadController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/__HairibarCertification/HairibarCertification.controller");
            Assert.That(controller, Is.Not.Null,
                "Run HairibarCertification.PrepareAssets before certification tests.");
            return controller;
        }
    }
}
