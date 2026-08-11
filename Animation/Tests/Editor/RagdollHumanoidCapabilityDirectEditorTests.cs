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
                { HumanBodyBones.Spine, references.spine },
                { HumanBodyBones.Head, references.head },
                { HumanBodyBones.LeftUpperArm, references.leftUpperArm },
                { HumanBodyBones.LeftLowerArm, references.leftLowerArm },
                { HumanBodyBones.LeftHand, references.leftHand },
                { HumanBodyBones.RightUpperArm, references.rightUpperArm },
                { HumanBodyBones.RightLowerArm, references.rightLowerArm },
                { HumanBodyBones.RightHand, references.rightHand },
                { HumanBodyBones.LeftUpperLeg, references.leftUpperLeg },
                { HumanBodyBones.LeftLowerLeg, references.leftLowerLeg },
                { HumanBodyBones.LeftFoot, references.leftFoot },
                { HumanBodyBones.RightUpperLeg, references.rightUpperLeg },
                { HumanBodyBones.RightLowerLeg, references.rightLowerLeg },
                { HumanBodyBones.RightFoot, references.rightFoot }
            };
            foreach (KeyValuePair<HumanBodyBones, Transform> pair in required)
            {
                Assert.That(pair.Value,
                    Is.SameAs(animator.GetBoneTransform(pair.Key)), pair.Key.ToString());
            }
            Assert.That(required.Values.Distinct().Count(),
                Is.EqualTo(required.Count));
            Transform expectedChest = animator.GetBoneTransform(HumanBodyBones.Chest)
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Assert.That(references.chest, Is.SameAs(expectedChest));

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
            Assert.That(hips.name, Is.Not.EqualTo("Root"));
            Assert.That(spine.name, Is.Not.EqualTo("Child"));
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
            Transform committedTarget = result.Target;
            RagdollTargetBindings committedBindings =
                result.Animator.TargetBindings;

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
            Assert.That(result.Target, Is.SameAs(committedTarget));
            Assert.That(result.Target, Is.SameAs(animator.transform));
            Assert.That(result.Animator.TargetBindings,
                Is.SameAs(committedBindings));
            Assert.That(committedBindings.TryGetBinding(
                new BoneName("Root"), out RagdollTargetBinding retainedRoot),
                Is.True);
            Assert.That(retainedRoot.Target, Is.SameAs(hips));
        }

        [Test]
        public void I02_PublicHumanoidBakeCommitsUsableSemanticClipAndInvalidAvatarRollsBack()
        {
            Animator animator = InstantiateHumanoid();
            AnimationClip source = Load<AnimationClip>(
                GeneratedRoot + "/HairibarCertificationLocomotion.anim");
            AnimationClip output = BakeClip(animator, source, "_I02", true);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(output);
            Assert.That(output.humanMotion, Is.True,
                "The committed output must remain a Unity Humanoid clip.");
            Assert.That(output.legacy, Is.False);
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(output),
                Is.Empty);
            Assert.That(bindings.Any(value => value.type == typeof(Animator)
                && HumanTrait.MuscleName.Contains(value.propertyName)), Is.True);
            AssertAnimatorCurve(output, "RootT.x", true);
            AssertAnimatorCurve(output, "RootT.y", true);
            AssertAnimatorCurve(output, "RootT.z", true);
            AssertAnimatorCurve(output, "RootQ.x", true);
            AssertAnimatorCurve(output, "RootQ.y", true);
            AssertAnimatorCurve(output, "RootQ.z", true);
            AssertAnimatorCurve(output, "RootQ.w", true);
            AssertAllFloatCurvesFinite(output);

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
            AssertGoal(withHands, properties, "LeftFoot");
            AssertGoal(withHands, properties, "RightFoot");
            AssertGoal(withHands, properties, "LeftHand");
            AssertGoal(withHands, properties, "RightHand");
            AssertAllFloatCurvesFinite(withHands);

            AnimationClip withoutHands =
                BakeClip(animator, source, "_I08FeetOnly", false);
            string[] feetOnly = AnimationUtility.GetCurveBindings(withoutHands)
                .Select(value => value.propertyName).ToArray();
            AssertGoal(withoutHands, feetOnly, "LeftFoot");
            AssertGoal(withoutHands, feetOnly, "RightFoot");
            Assert.That(feetOnly.Any(value => value.StartsWith("LeftHand")),
                Is.False);
            Assert.That(feetOnly.Any(value => value.StartsWith("RightHand")),
                Is.False);
        }

        [Test]
        public void I09_MultilayerControllerBakeCommitsRootAndUpperBodyAndRetargets()
        {
            Animator first = InstantiateHumanoid();
            Assert.That(first.layerCount, Is.GreaterThanOrEqualTo(2));
            first.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            first.Rebind();
            first.Update(0f);
            first.SetLayerWeight(1, 1f);
            AnimationClip upperSource = Load<AnimationClip>(
                GeneratedRoot + "/HairibarCertificationUpperBody.anim");
            string armMuscle = HumanTrait.MuscleName.First(value =>
                value.IndexOf(
                    "Left Arm",
                    StringComparison.OrdinalIgnoreCase) >= 0);
            AnimationCurve upperArmCurve = AnimationUtility.GetEditorCurve(
                upperSource,
                EditorCurveBinding.FloatCurve(
                    string.Empty, typeof(Animator), armMuscle));
            Assert.That(upperSource.humanMotion, Is.True);
            Assert.That(upperArmCurve, Is.Not.Null);
            Assert.That(CurveRange(upperArmCurve), Is.GreaterThan(0.5f),
                "The certification upper-body source itself is not variable.");

            Animator directClipProbe = InstantiateHumanoid();
            directClipProbe.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            directClipProbe.enabled = false;
            Transform directArm = directClipProbe.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            float directInitialMuscle;
            float directAdvancedMuscle;
            Quaternion directInitialRotation;
            Quaternion directAdvancedRotation;
            int armMuscleIndex = Array.IndexOf(
                HumanTrait.MuscleName, armMuscle);
            Assert.That(armMuscleIndex, Is.GreaterThanOrEqualTo(0));
            using (HumanPoseHandler directHandler = new HumanPoseHandler(
                directClipProbe.avatar, directClipProbe.avatarRoot))
            {
                HumanPose directPose = new HumanPose();
                upperSource.SampleAnimation(directClipProbe.gameObject, 0f);
                directHandler.GetHumanPose(ref directPose);
                directInitialMuscle = directPose.muscles[armMuscleIndex];
                directInitialRotation = directArm.localRotation;
                upperSource.SampleAnimation(
                    directClipProbe.gameObject, upperSource.length * 0.5f);
                directHandler.GetHumanPose(ref directPose);
                directAdvancedMuscle = directPose.muscles[armMuscleIndex];
                directAdvancedRotation = directArm.localRotation;
            }
            Assert.That(Mathf.Abs(
                    directAdvancedMuscle - directInitialMuscle),
                Is.GreaterThan(0.05f),
                "Unity did not evaluate the generated Humanoid muscle curve "
                + "when the source clip was sampled directly.");
            Assert.That(Quaternion.Angle(
                    directInitialRotation, directAdvancedRotation),
                Is.GreaterThan(0.05f),
                "The directly sampled Humanoid source did not move LeftUpperArm.");

            first.Play("Locomotion", 0, 0f);
            first.Play("Waving", 1, 0f);
            first.Update(0f);
            AnimatorStateInfo upperState =
                first.GetCurrentAnimatorStateInfo(1);
            Assert.That(upperState.IsName("Waving"),
                Is.True,
                "Layer 1 did not enter the real upper-body state.");
            Assert.That(first.GetLayerWeight(1),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(upperState.length,
                Is.EqualTo(upperSource.length).Within(0.001f),
                "Layer 1 is not evaluating the generated upper-body motion.");
            Transform controllerArm = first.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            Quaternion controllerInitialRotation = controllerArm.localRotation;
            float initialArm;
            float advancedArm;
            using (HumanPoseHandler poseHandler = new HumanPoseHandler(
                first.avatar, first.avatarRoot))
            {
                HumanPose pose = new HumanPose();
                poseHandler.GetHumanPose(ref pose);
                initialArm = pose.muscles[armMuscleIndex];
                first.Update(upperSource.length * 0.5f);
                poseHandler.GetHumanPose(ref pose);
                advancedArm = pose.muscles[armMuscleIndex];
            }
            Assert.That(Quaternion.Angle(
                    controllerInitialRotation, controllerArm.localRotation),
                Is.GreaterThan(0.05f),
                "The effective layer state and weight did not move LeftUpperArm.");
            Assert.That(Mathf.Abs(advancedArm - initialArm),
                Is.GreaterThan(0.05f),
                "The real AnimatorController/AvatarMask did not produce an "
                + "observable upper-body Humanoid pose before Baker ran.");

            first.Play("Locomotion", 0, 0f);
            first.Play("Waving", 1, 0f);
            first.Update(0f);
            HumanoidEditorAnimationEventProbe eventProbe =
                first.GetComponent<HumanoidEditorAnimationEventProbe>();
            int eventsBeforeBake = eventProbe.Count;
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
            Assert.That(eventProbe.Count, Is.GreaterThan(eventsBeforeBake),
                "The base-state AnimationEvent must be evaluated by the real "
                + "AnimatorController during the state bake.");
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                OutputRoot + "/Locomotion_I09.anim");
            Assert.That(output, Is.Not.Null);
            string[] properties = AnimationUtility.GetCurveBindings(output)
                .Select(value => value.propertyName).ToArray();
            Assert.That(properties, Does.Contain("RootT.x"));
            string varyingArmCurve = FindVaryingMuscleCurve(output, "Arm");
            Assert.That(varyingArmCurve, Is.Not.Null.And.Not.Empty,
                "The upper-body layer must affect an arm muscle over time; "
                + "the mere presence of a recorder-created binding is not evidence "
                + "that the layer was evaluated.");
            AssertAllFloatCurvesFinite(output);

            Animator second = InstantiateHumanoid();
            Transform secondHips = second.GetBoneTransform(HumanBodyBones.Hips);
            Transform secondArm = second.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            second.enabled = false;
            Quaternion before = secondHips.localRotation;
            Quaternion armBefore = secondArm.localRotation;
            output.SampleAnimation(second.gameObject, output.length * 0.6f);
            Assert.That(Quaternion.Angle(before, secondHips.localRotation),
                Is.GreaterThan(0.001f),
                "The committed Humanoid clip did not retarget to a second Avatar.");
            Assert.That(Quaternion.Angle(armBefore, secondArm.localRotation),
                Is.GreaterThan(0.001f),
                "The recorded upper-body result did not retarget to the second Avatar.");
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

        static void AssertGoal(
            AnimationClip clip,
            IEnumerable<string> properties,
            string goal)
        {
            Assert.That(properties, Does.Contain(goal + "T.x"));
            Assert.That(properties, Does.Contain(goal + "T.y"));
            Assert.That(properties, Does.Contain(goal + "T.z"));
            Assert.That(properties, Does.Contain(goal + "Q.x"));
            Assert.That(properties, Does.Contain(goal + "Q.y"));
            Assert.That(properties, Does.Contain(goal + "Q.z"));
            Assert.That(properties, Does.Contain(goal + "Q.w"));
            AssertAnimatorCurve(clip, goal + "T.x", true);
            AssertAnimatorCurve(clip, goal + "T.y", true);
            AssertAnimatorCurve(clip, goal + "T.z", true);
            AssertAnimatorCurve(clip, goal + "Q.x", true);
            AssertAnimatorCurve(clip, goal + "Q.y", true);
            AssertAnimatorCurve(clip, goal + "Q.z", true);
            AssertAnimatorCurve(clip, goal + "Q.w", true);
        }

        static void AssertAnimatorCurve(
            AnimationClip clip,
            string property,
            bool requireEndpoints)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                string.Empty, typeof(Animator), property);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.That(curve, Is.Not.Null, property);
            Assert.That(curve.length, Is.GreaterThanOrEqualTo(
                requireEndpoints && clip.length > 0f ? 2 : 1), property);
            Keyframe[] keys = curve.keys;
            for (int index = 0; index < keys.Length; index++)
            {
                Assert.That(IsFinite(keys[index].time), Is.True,
                    property + " key time " + index);
                Assert.That(IsFinite(keys[index].value), Is.True,
                    property + " key value " + index);
            }
            if (!requireEndpoints || clip.length <= 0f) return;
            Assert.That(keys[0].time, Is.EqualTo(0f).Within(0.00001f), property);
            Assert.That(keys[keys.Length - 1].time,
                Is.EqualTo(clip.length).Within(0.0001f), property);
        }

        static void AssertAllFloatCurvesFinite(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings, Is.Not.Empty);
            for (int bindingIndex = 0;
                bindingIndex < bindings.Length;
                bindingIndex++)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    clip, bindings[bindingIndex]);
                Assert.That(curve, Is.Not.Null, bindings[bindingIndex].propertyName);
                Keyframe[] keys = curve.keys;
                Assert.That(keys, Is.Not.Empty, bindings[bindingIndex].propertyName);
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    Assert.That(IsFinite(keys[keyIndex].time), Is.True,
                        bindings[bindingIndex].propertyName + " time " + keyIndex);
                    Assert.That(IsFinite(keys[keyIndex].value), Is.True,
                        bindings[bindingIndex].propertyName + " value " + keyIndex);
                }
            }
        }

        static string FindVaryingMuscleCurve(
            AnimationClip clip,
            string semanticToken)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int index = 0; index < bindings.Length; index++)
            {
                EditorCurveBinding binding = bindings[index];
                if (binding.type != typeof(Animator)
                    || !HumanTrait.MuscleName.Contains(binding.propertyName)
                    || binding.propertyName.IndexOf(
                        semanticToken,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;
                float minimum = curve.keys[0].value;
                float maximum = minimum;
                for (int keyIndex = 1; keyIndex < curve.length; keyIndex++)
                {
                    minimum = Mathf.Min(minimum, curve.keys[keyIndex].value);
                    maximum = Mathf.Max(maximum, curve.keys[keyIndex].value);
                }
                if (maximum - minimum > 0.0001f) return binding.propertyName;
            }
            return string.Empty;
        }

        static float CurveRange(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            Keyframe[] keys = curve.keys;
            float minimum = keys[0].value;
            float maximum = minimum;
            for (int index = 1; index < keys.Length; index++)
            {
                minimum = Mathf.Min(minimum, keys[index].value);
                maximum = Mathf.Max(maximum, keys[index].value);
            }
            return maximum - minimum;
        }

        Animator InstantiateHumanoid()
        {
            GameObject value = InstantiateAsset<GameObject>(
                RuntimeRoot + "/Humanoid.prefab");
            Animator animator = value.GetComponentInChildren<Animator>(true);
            Assert.That(animator && animator.avatar && animator.avatar.isValid
                && animator.avatar.isHuman, Is.True);
            if (!animator.GetComponent<HumanoidEditorAnimationEventProbe>())
                animator.gameObject.AddComponent<HumanoidEditorAnimationEventProbe>();
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

    public sealed class HumanoidEditorAnimationEventProbe : MonoBehaviour
    {
        public int Count { get; private set; }

        public void OnHairibarCertificationAnimationEvent()
        {
            Count++;
        }
    }
}
