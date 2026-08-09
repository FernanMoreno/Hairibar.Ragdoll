using System;
using System.Collections.Generic;
using System.Linq;
using Hairibar.Ragdoll.Animation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollHumanoidCapabilityDirectEditorTests
    {
        const string GeneratedRoot = "Assets/__HairibarCertification";
        const string RuntimeRoot = GeneratedRoot
            + "/Resources/HairibarCertification";
        const string OutputRoot = "Assets/__HairibarHumanoidDirectTests";
        readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();
        bool ignoredBefore;
        bool alternateIgnoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            alternateIgnoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            EnsurePrepared();
            if (AssetDatabase.IsValidFolder(OutputRoot))
                AssetDatabase.DeleteAsset(OutputRoot);
            AssetDatabase.CreateFolder("Assets", "__HairibarHumanoidDirectTests");
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            Physics.IgnoreLayerCollision(28, 29, alternateIgnoredBefore);
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index])
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            owned.Clear();
            if (AssetDatabase.IsValidFolder(OutputRoot))
                AssetDatabase.DeleteAsset(OutputRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void A02_HumanoidDiscoveryResolvesEveryRequiredSemanticAndRejectsInvalidAvatar()
        {
            Animator animator = InstantiateHumanoid();
            RagdollBipedReferences references;
            string error;
            Assert.That(RagdollBipedReferences.TryFromHumanoid(
                animator, out references, out error), Is.True, error);

            var required = new Dictionary<HumanBodyBones, Transform>
            {
                { HumanBodyBones.Hips, references.hips },
                { HumanBodyBones.Head, references.head },
                { HumanBodyBones.LeftUpperArm, references.leftUpperArm },
                { HumanBodyBones.LeftLowerArm, references.leftLowerArm },
                { HumanBodyBones.RightUpperArm, references.rightUpperArm },
                { HumanBodyBones.RightLowerArm, references.rightLowerArm },
                { HumanBodyBones.LeftUpperLeg, references.leftUpperLeg },
                { HumanBodyBones.LeftLowerLeg, references.leftLowerLeg },
                { HumanBodyBones.RightUpperLeg, references.rightUpperLeg },
                { HumanBodyBones.RightLowerLeg, references.rightLowerLeg }
            };
            foreach (KeyValuePair<HumanBodyBones, Transform> pair in required)
            {
                Assert.That(pair.Value,
                    Is.SameAs(animator.GetBoneTransform(pair.Key)), pair.Key.ToString());
            }
            Assert.That(required.Values.Distinct().Count(),
                Is.EqualTo(required.Count));

            int renameIndex = 0;
            foreach (Transform bone in references.EnumerateAll())
                if (bone) bone.name = "SemanticBone_" + renameIndex++;
            Assert.That(RagdollBipedReferences.TryFromHumanoid(
                animator, out RagdollBipedReferences renamed, out error),
                Is.True, error);
            Assert.That(renamed.hips,
                Is.SameAs(animator.GetBoneTransform(HumanBodyBones.Hips)));

            GameObject invalidObject = Own(new GameObject("Invalid Humanoid"));
            Animator invalid = invalidObject.AddComponent<Animator>();
            Assert.That(RagdollBipedReferences.TryFromHumanoid(
                invalid, out RagdollBipedReferences rejected, out error),
                Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void A12_SemanticSetupBindsRenamedReorientedTargetAndRollsBackInvalidAvatar()
        {
            GameObject rig = InstantiateAsset<GameObject>(
                RuntimeRoot + "/RegressionRig.prefab");
            RagdollDefinitionBindings puppet =
                rig.GetComponentInChildren<RagdollDefinitionBindings>(true);
            Assert.That(puppet && puppet.IsInitialized, Is.True);
            RagdollAnimationProfile animationProfile = Load<RagdollAnimationProfile>(
                RuntimeRoot + "/RegressionProfile.asset");
            RagdollHumanoidBindingProfile semantic =
                Load<RagdollHumanoidBindingProfile>(
                    RuntimeRoot + "/HumanoidBindings.asset");
            Animator animator = InstantiateHumanoid();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            hips.name = "Alternate_Hips_NoNameMatch";
            spine.name = "Alternate_Spine_NoNameMatch";
            hips.localRotation *= Quaternion.Euler(13f, -21f, 8f);
            spine.localRotation *= Quaternion.Euler(-9f, 17f, 5f);
            Quaternion hipsAuthored = hips.localRotation;
            Quaternion spineAuthored = spine.localRotation;

            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    animator.transform,
                    puppet,
                    animationProfile,
                    semantic,
                    30,
                    31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Animator.TargetBindings.TryGetBinding(
                new BoneName("Root"), out RagdollTargetBinding rootBinding),
                Is.True);
            Assert.That(rootBinding.Target, Is.SameAs(hips));
            Assert.That(result.Animator.TargetBindings.TryGetBinding(
                new BoneName("Child"), out RagdollTargetBinding childBinding),
                Is.True);
            Assert.That(childBinding.Target, Is.SameAs(spine));
            Assert.That(Quaternion.Angle(hips.localRotation, hipsAuthored),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(spine.localRotation, spineAuthored),
                Is.LessThan(0.001f));

            GameObject invalid = Own(new GameObject("Non Humanoid Target"));
            invalid.layer = 6;
            invalid.AddComponent<Animator>();
            RagdollSetupResult rejected =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    invalid.transform,
                    puppet,
                    animationProfile,
                    semantic,
                    28,
                    29);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Error, Is.Not.Empty);
            Assert.That(invalid.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(invalid.GetComponent<RagdollTargetBindings>(), Is.Null);
            Assert.That(invalid.layer, Is.EqualTo(6));
            Assert.That(Physics.GetIgnoreLayerCollision(28, 29),
                Is.EqualTo(alternateIgnoredBefore));
        }

        [Test]
        public void I02_PublicHumanoidBakeCommitsUsableSemanticClipAndInvalidAvatarRollsBack()
        {
            Animator animator = InstantiateHumanoid();
            AnimationClip source = Load<AnimationClip>(
                GeneratedRoot + "/HairibarCertificationLocomotion.anim");
            AnimationClip output = BakeClip(animator, source, "_I02", true);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(output);
            Assert.That(bindings.Any(value => value.type == typeof(Animator)
                && HumanTrait.MuscleName.Contains(value.propertyName)), Is.True);
            Assert.That(bindings.Any(value => value.propertyName == "RootT.x"),
                Is.True);

            using (HumanPoseHandler handler = new HumanPoseHandler(
                animator.avatar, animator.avatarRoot))
            {
                output.SampleAnimation(animator.gameObject, output.length);
                HumanPose pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Assert.That(pose.muscles, Has.Length.EqualTo(HumanTrait.MuscleCount));
                Assert.That(pose.muscles.All(IsFinite), Is.True);
            }

            string protectedPath = OutputRoot + "/" + source.name
                + "_Invalid.anim";
            AnimationClip protectedClip = new AnimationClip();
            protectedClip.SetCurve("", typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Constant(0f, 1f, 17f));
            AssetDatabase.CreateAsset(protectedClip, protectedPath);
            GameObject invalidObject = Own(new GameObject("Invalid Baker Avatar"));
            Animator invalidAnimator = invalidObject.AddComponent<Animator>();
            RagdollHumanoidBaker invalidBaker =
                invalidObject.AddComponent<RagdollHumanoidBaker>();
            Configure(invalidBaker, source, "_Invalid", string.Empty);
            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                invalidBaker, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            AnimationClip unchanged =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(protectedPath);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(
                unchanged,
                EditorCurveBinding.FloatCurve(
                    "", typeof(Transform), "m_LocalPosition.x"));
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(17f));
        }

        [Test]
        public void I08_PublicHumanoidBakeWritesFeetAndOptionalHandsToCommittedAsset()
        {
            Animator animator = InstantiateHumanoid();
            AnimationClip source = Load<AnimationClip>(
                GeneratedRoot + "/HairibarCertificationLocomotion.anim");
            AnimationClip withHands = BakeClip(animator, source, "_I08Hands", true);
            string[] properties = AnimationUtility.GetCurveBindings(withHands)
                .Select(value => value.propertyName).ToArray();
            AssertGoal(properties, "LeftFoot");
            AssertGoal(properties, "RightFoot");
            AssertGoal(properties, "LeftHand");
            AssertGoal(properties, "RightHand");

            AnimationClip withoutHands =
                BakeClip(animator, source, "_I08FeetOnly", false);
            string[] feetOnly = AnimationUtility.GetCurveBindings(withoutHands)
                .Select(value => value.propertyName).ToArray();
            AssertGoal(feetOnly, "LeftFoot");
            AssertGoal(feetOnly, "RightFoot");
            Assert.That(feetOnly.Any(value => value.StartsWith("LeftHand")),
                Is.False);
            Assert.That(feetOnly.Any(value => value.StartsWith("RightHand")),
                Is.False);
        }

        [Test]
        public void I09_MultilayerControllerBakeCommitsRootAndUpperBodyAndRetargets()
        {
            Animator first = InstantiateHumanoid();
            RagdollHumanoidBaker baker =
                first.gameObject.AddComponent<RagdollHumanoidBaker>();
            baker.mode = RagdollBakerMode.AnimationStates;
            baker.animationStates = new[] { "Locomotion" };
            baker.frameRate = 30;
            baker.keyReductionError = 0f;
            baker.IKKeyReductionError = 0f;
            baker.appendName = "_I09";
            baker.saveToFolder = OutputRoot;
            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error), Is.True, error);
            Assert.That(baker.LastResult.Succeeded, Is.True, baker.LastResult.Error);
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                OutputRoot + "/Locomotion_I09.anim");
            Assert.That(output, Is.Not.Null);
            string[] properties = AnimationUtility.GetCurveBindings(output)
                .Select(value => value.propertyName).ToArray();
            Assert.That(properties, Does.Contain("RootT.x"));
            Assert.That(properties.Any(value =>
                value.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, "The upper-body layer produced no recorded arm muscle.");

            Animator second = InstantiateHumanoid();
            Transform secondHips = second.GetBoneTransform(HumanBodyBones.Hips);
            second.enabled = false;
            Quaternion before = secondHips.localRotation;
            output.SampleAnimation(second.gameObject, output.length * 0.6f);
            Assert.That(Quaternion.Angle(before, secondHips.localRotation),
                Is.GreaterThan(0.001f),
                "The committed Humanoid clip did not retarget to a second Avatar.");
            Assert.That(AnimationUtility.GetAnimationEvents(
                Load<AnimationClip>(GeneratedRoot
                    + "/HairibarCertificationLocomotion.anim")),
                Is.Not.Empty,
                "The real controller source must contain an Animator event for the PlayMode companion.");
        }

        AnimationClip BakeClip(
            Animator animator,
            AnimationClip source,
            string suffix,
            bool hands)
        {
            RagdollHumanoidBaker baker =
                animator.gameObject.AddComponent<RagdollHumanoidBaker>();
            Configure(baker, source, suffix, string.Empty);
            baker.bakeHandIK = hands;
            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error), Is.True, error);
            Assert.That(baker.LastResult.Succeeded, Is.True, baker.LastResult.Error);
            string path = OutputRoot + "/" + source.name + suffix + ".anim";
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(output, Is.Not.Null, path);
            return output;
        }

        static void Configure(
            RagdollHumanoidBaker baker,
            AnimationClip source,
            string suffix,
            string saveName)
        {
            baker.mode = RagdollBakerMode.AnimationClips;
            baker.animationClips = new[] { source };
            baker.frameRate = 30;
            baker.keyReductionError = 0f;
            baker.IKKeyReductionError = 0f;
            baker.appendName = suffix;
            baker.saveName = saveName;
            baker.saveToFolder = OutputRoot;
        }

        static void AssertGoal(IEnumerable<string> properties, string goal)
        {
            Assert.That(properties, Does.Contain(goal + "T.x"));
            Assert.That(properties, Does.Contain(goal + "T.y"));
            Assert.That(properties, Does.Contain(goal + "T.z"));
            Assert.That(properties, Does.Contain(goal + "Q.w"));
        }

        Animator InstantiateHumanoid()
        {
            GameObject value = InstantiateAsset<GameObject>(
                RuntimeRoot + "/Humanoid.prefab");
            Animator animator = value.GetComponentInChildren<Animator>(true);
            Assert.That(animator && animator.avatar && animator.avatar.isValid
                && animator.avatar.isHuman, Is.True);
            return animator;
        }

        T InstantiateAsset<T>(string path) where T : UnityEngine.Object
        {
            T source = Load<T>(path);
            T value = UnityEngine.Object.Instantiate(source);
            owned.Add(value);
            return value;
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null,
                "Run HairibarCertification.PrepareAssets before certification tests: "
                + path);
            return value;
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static void EnsurePrepared()
        {
            Assert.That(AssetDatabase.IsValidFolder(RuntimeRoot), Is.True,
                "Run HairibarCertification.PrepareAssets before certification tests.");
        }
    }
}
