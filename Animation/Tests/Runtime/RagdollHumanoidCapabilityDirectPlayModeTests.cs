using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollHumanoidCapabilityDirectPlayModeTests
    {
        const string ResourceRoot = "HairibarCertification/";
        readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();
        bool ignoredBefore;
        float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            originalTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = originalTimeScale;
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index]) UnityEngine.Object.Destroy(owned[index]);
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator B22_SharedHumanoidProfileBindsTwoRenamedAvatarsSemantically()
        {
            GameObject rig = InstantiateResource<GameObject>("RegressionRig");
            RagdollDefinitionBindings puppet =
                rig.GetComponentInChildren<RagdollDefinitionBindings>(true);
            Assert.That(puppet && puppet.IsInitialized, Is.True);
            RagdollHumanoidBindingProfile profile =
                LoadResource<RagdollHumanoidBindingProfile>("HumanoidBindings");
            Animator first = InstantiateHumanoid();
            Animator second = InstantiateHumanoid();
            yield return null;

            Transform firstHips = first.GetBoneTransform(HumanBodyBones.Hips);
            Transform firstSpine = first.GetBoneTransform(HumanBodyBones.Spine);
            Transform secondHips = second.GetBoneTransform(HumanBodyBones.Hips);
            Transform secondSpine = second.GetBoneTransform(HumanBodyBones.Spine);
            firstHips.name = "FirstSemanticPelvis";
            firstSpine.name = "FirstSemanticSpine";
            secondHips.name = "SecondSemanticPelvis";
            secondSpine.name = "SecondSemanticSpine";
            secondHips.localRotation *= Quaternion.Euler(11f, 23f, -7f);
            secondSpine.localRotation *= Quaternion.Euler(-5f, 19f, 13f);

            RagdollTargetBindings firstBindings =
                first.gameObject.AddComponent<RagdollTargetBindings>();
            firstBindings.SetRagdollBindings(puppet);
            RagdollTargetBindings secondBindings =
                second.gameObject.AddComponent<RagdollTargetBindings>();
            secondBindings.SetRagdollBindings(puppet);
            string error;
            Assert.That(profile.TryApply(first, firstBindings, out error),
                Is.True, error);
            Assert.That(firstBindings.TryCaptureOffsets(out error), Is.True, error);
            Assert.That(profile.TryApply(second, secondBindings, out error),
                Is.True, error);
            Assert.That(secondBindings.TryCaptureOffsets(out error), Is.True, error);
            AssertSemanticTarget(firstBindings, "Root", firstHips);
            AssertSemanticTarget(firstBindings, "Child", firstSpine);
            AssertSemanticTarget(secondBindings, "Root", secondHips);
            AssertSemanticTarget(secondBindings, "Child", secondSpine);

            GameObject invalidObject = Own(new GameObject("Invalid Avatar"));
            Animator invalid = invalidObject.AddComponent<Animator>();
            RagdollTargetBindings rejected =
                invalidObject.AddComponent<RagdollTargetBindings>();
            rejected.SetRagdollBindings(puppet);
            Assert.That(profile.TryApply(invalid, rejected, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(rejected.Bindings, Is.Empty,
                "A rejected Avatar must not partially replace the binding table.");
        }

        [UnityTest]
        public IEnumerator E01_TargetAnimatorFallRunsBlendRuntimeSettersAndBothEndGates()
        {
            GameObject rig = InstantiateResource<GameObject>("RegressionRig");
            RagdollDefinitionBindings puppet =
                rig.GetComponentInChildren<RagdollDefinitionBindings>(true);
            RagdollAnimationProfile animationProfile =
                LoadResource<RagdollAnimationProfile>("RegressionProfile");
            RagdollHumanoidBindingProfile semantic =
                LoadResource<RagdollHumanoidBindingProfile>("HumanoidBindings");
            Animator targetAnimator = InstantiateHumanoid();
            Rigidbody pelvis = puppet.GetComponent<Rigidbody>();
            pelvis.position = Vector3.up * 2f;
            pelvis.useGravity = false;

            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    targetAnimator.transform,
                    puppet,
                    animationProfile,
                    semantic,
                    30,
                    31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            result.Animator.TargetAnimator = targetAnimator;
            RagdollFallBehaviour fall = result.Behaviours.BehaviourRoot
                .gameObject.AddComponent<RagdollFallBehaviour>();
            fall.StateName = "Fall";
            fall.Layer = 0;
            fall.TransitionDuration = 0f;
            fall.FixedTime = 0f;
            fall.BlendParameter = "FallBlend";
            fall.RaycastLayers = ~0;
            fall.WritheHeight = 4f;
            fall.WritheYVelocity = 1f;
            fall.BlendSpeed = 100f;
            fall.BlendMappingSpeed = 100f;
            fall.CanEnd = true;
            fall.MinimumTime = 0.12f;
            fall.MaximumEndVelocity = 0.1f;
            int serializedEnds = 0;
            int runtimeEnds = 0;
            var unityEnd = new UnityEvent();
            unityEnd.AddListener(() => serializedEnds++);
            RagdollPuppetEvent endEvent = fall.OnEnd;
            endEvent.UnityEvent = unityEnd;
            fall.OnEnd = endEvent;
            fall.Ended += () => runtimeEnds++;

            GameObject ground = Own(GameObject.CreatePrimitive(PrimitiveType.Plane));
            ground.name = "Fall Ground";
            ground.transform.position = Vector3.zero;
            yield return null;
            Assert.That(result.Animator.Initiated, Is.True);
            Assert.That(fall.IsInitialized, Is.True);
            Assert.That(fall.Activate(), Is.True);
            pelvis.linearVelocity = Vector3.up * 2f;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(targetAnimator.GetCurrentAnimatorStateInfo(0).IsName("Fall"),
                Is.True);
            Assert.That(fall.CurrentBlend, Is.InRange(0f, 1f));
            Assert.That(targetAnimator.GetFloat("FallBlend"),
                Is.EqualTo(fall.CurrentBlend).Within(0.001f));

            for (int step = 0; step < 8; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(fall.ElapsedTime, Is.GreaterThanOrEqualTo(fall.MinimumTime));
            Assert.That(fall.HasEnded, Is.False,
                "Minimum time alone must not bypass the velocity gate.");

            pelvis.linearVelocity = Vector3.zero;
            for (int step = 0; step < 8 && !fall.HasEnded; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(fall.HasEnded, Is.True);
            Assert.That(serializedEnds, Is.EqualTo(1));
            Assert.That(runtimeEnds, Is.EqualTo(1));
            for (int step = 0; step < 4; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(serializedEnds, Is.EqualTo(1));
            Assert.That(runtimeEnds, Is.EqualTo(1));

            fall.BlendParameter = string.Empty;
            fall.WritheHeight = float.NaN;
            fall.WritheYVelocity = -1f;
            fall.BlendSpeed = float.PositiveInfinity;
            Assert.That(fall.WritheHeight, Is.Zero);
            Assert.That(fall.WritheYVelocity, Is.Zero);
            Assert.That(fall.BlendSpeed, Is.Zero);
            fall.BlendParameter = "FallBlend";
            Assert.That(fall.BlendParameter, Is.EqualTo("FallBlend"));
        }

        [UnityTest]
        public IEnumerator MultilayerAnimatorModesEventsRootMotionAndRetargeting()
        {
            Animator first = InstantiateHumanoid();
            Animator second = InstantiateHumanoid();
            first.applyRootMotion = true;
            second.applyRootMotion = true;
            HumanoidAnimationEventProbe probe =
                first.gameObject.AddComponent<HumanoidAnimationEventProbe>();
            yield return null;

            AnimatorUpdateMode[] modes =
            {
                AnimatorUpdateMode.Normal,
                AnimatorUpdateMode.Fixed,
                AnimatorUpdateMode.UnscaledTime
            };
            float[] scales = { 0f, 0.5f, 1f, 2f };
            for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
            {
                first.updateMode = modes[modeIndex];
                for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
                {
                    Time.timeScale = scales[scaleIndex];
                    first.Play("Locomotion", 0, 0f);
                    first.Update(0f);
                    float before = first.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    for (int frame = 0; frame < 8; frame++) yield return null;
                    float after = first.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    Assert.That(IsFinite(after), Is.True,
                        modes[modeIndex] + " @ " + scales[scaleIndex]);
                    if (scales[scaleIndex] == 0f)
                    {
                        bool unscaled = modes[modeIndex]
                            == AnimatorUpdateMode.UnscaledTime;
                        Assert.That(unscaled ? after > before
                            : Mathf.Abs(after - before) < 0.0001f, Is.True,
                            modes[modeIndex] + " @ timeScale 0");
                    }
                    else
                    {
                        Assert.That(after, Is.GreaterThan(before),
                            modes[modeIndex] + " @ " + scales[scaleIndex]);
                    }
                }
            }

            Time.timeScale = 1f;
            first.updateMode = AnimatorUpdateMode.Normal;
            Vector3 rootBefore = first.transform.position;
            int eventBefore = probe.Count;
            first.Play("Locomotion", 0, 0f);
            first.Update(0f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (probe.Count == eventBefore
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(probe.Count, Is.GreaterThan(eventBefore));
            Assert.That(Vector3.Distance(rootBefore, first.transform.position),
                Is.GreaterThan(0.0001f));

            first.Play("Locomotion", 0, 0.35f);
            second.Play("Locomotion", 0, 0.35f);
            first.Update(0f);
            second.Update(0f);
            yield return null;
            Transform firstHips = first.GetBoneTransform(HumanBodyBones.Hips);
            Transform secondHips = second.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(first.layerCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(second.layerCount, Is.EqualTo(first.layerCount));
            Assert.That(Quaternion.Angle(
                firstHips.localRotation, secondHips.localRotation),
                Is.LessThan(0.1f));
        }

        Animator InstantiateHumanoid()
        {
            GameObject value = InstantiateResource<GameObject>("Humanoid");
            Animator animator = value.GetComponentInChildren<Animator>(true);
            Assert.That(animator && animator.avatar && animator.avatar.isValid
                && animator.avatar.isHuman, Is.True,
                "Run HairibarCertification.PrepareAssets before PlayMode tests.");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            return animator;
        }

        T InstantiateResource<T>(string name) where T : UnityEngine.Object
        {
            T value = UnityEngine.Object.Instantiate(LoadResource<T>(name));
            owned.Add(value);
            return value;
        }

        static T LoadResource<T>(string name) where T : UnityEngine.Object
        {
            T value = Resources.Load<T>(ResourceRoot + name);
            Assert.That(value, Is.Not.Null,
                "Run HairibarCertification.PrepareAssets before PlayMode tests: "
                + name);
            return value;
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        static void AssertSemanticTarget(
            RagdollTargetBindings bindings,
            string bone,
            Transform expected)
        {
            Assert.That(bindings.TryGetBinding(
                new BoneName(bone), out RagdollTargetBinding binding), Is.True);
            Assert.That(binding.Target, Is.SameAs(expected));
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class HumanoidAnimationEventProbe : MonoBehaviour
    {
        public int Count { get; private set; }

        public void OnHairibarCertificationAnimationEvent()
        {
            Count++;
        }
    }
}
