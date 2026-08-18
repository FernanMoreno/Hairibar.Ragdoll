using System.Collections.Generic;
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public class RagdollBakerPolicyTests
    {
        [Test]
        public void IgnoreList_DoesNotSuppressExplicitPositionBinding()
        {
            GameObject valueObject = new GameObject("Value");
            try
            {
                Transform value = valueObject.transform;
                HashSet<Transform> ignored = new HashSet<Transform> { value };
                HashSet<Transform> positions = new HashSet<Transform> { value };

                Assert.That(
                    RagdollGenericBakerBindingPolicy.ShouldBindRotation(
                        value,
                        ignored),
                    Is.False);
                Assert.That(
                    RagdollGenericBakerBindingPolicy.ShouldBindPosition(
                        value,
                        positions),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(valueObject);
            }
        }

        [Test]
        public void PreserveDestination_KeepsManualSettingsWhileApplyingLoop()
        {
            GameObject bakerObject = new GameObject("Baker");
            AnimationClip destination = new AnimationClip();
            try
            {
                RagdollGenericBaker baker =
                    bakerObject.AddComponent<RagdollGenericBaker>();
                baker.clipSettingsPolicy =
                    RagdollBakerClipSettingsPolicy.PreserveDestination;
                baker.loop = true;

                AnimationClipSettings authored =
                    AnimationUtility.GetAnimationClipSettings(destination);
                authored.cycleOffset = 0.37f;
                authored.loopBlend = true;
                AnimationUtility.SetAnimationClipSettings(destination, authored);

                RagdollBakerClipSettingsUtility.Apply(
                    baker,
                    null,
                    destination);

                AnimationClipSettings result =
                    AnimationUtility.GetAnimationClipSettings(destination);
                Assert.That(result.cycleOffset, Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(result.loopBlend, Is.True);
                Assert.That(result.loopTime, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(destination);
                Object.DestroyImmediate(bakerObject);
            }
        }

        [Test]
        public void InheritSource_ReplacesDestinationSettingsExplicitly()
        {
            GameObject bakerObject = new GameObject("Baker");
            AnimationClip source = new AnimationClip();
            AnimationClip destination = new AnimationClip();
            try
            {
                RagdollGenericBaker baker =
                    bakerObject.AddComponent<RagdollGenericBaker>();
                baker.clipSettingsPolicy =
                    RagdollBakerClipSettingsPolicy.InheritSource;

                AnimationClipSettings sourceSettings =
                    AnimationUtility.GetAnimationClipSettings(source);
                sourceSettings.cycleOffset = 0.72f;
                AnimationUtility.SetAnimationClipSettings(source, sourceSettings);

                RagdollBakerClipSettingsUtility.Apply(
                    baker,
                    source,
                    destination);

                AnimationClipSettings result =
                    AnimationUtility.GetAnimationClipSettings(destination);
                Assert.That(result.cycleOffset, Is.EqualTo(0.72f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(destination);
                Object.DestroyImmediate(bakerObject);
            }
        }

        [Test]
        public void GenericLoop_MatchesFinalValuesWithoutChangingKeyShape()
        {
            AnimationClip clip = new AnimationClip();
            try
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                    "Bone", typeof(Transform), "m_LocalPosition.x");
                Keyframe first = new Keyframe(0f, 2f, 3f, 4f)
                {
                    weightedMode = WeightedMode.Both,
                    inWeight = 0.2f,
                    outWeight = 0.3f
                };
                Keyframe last = new Keyframe(1f, 9f, 5f, 6f)
                {
                    weightedMode = WeightedMode.Both,
                    inWeight = 0.4f,
                    outWeight = 0.5f
                };
                AnimationUtility.SetEditorCurve(
                    clip, binding, new AnimationCurve(first, last));

                RagdollBakerLoopUtility.MatchEndKeysToStart(clip);

                Keyframe[] result = AnimationUtility
                    .GetEditorCurve(clip, binding).keys;
                Assert.That(result[1].value, Is.EqualTo(result[0].value));
                Assert.That(result[1].time, Is.EqualTo(1f));
                Assert.That(result[1].inTangent, Is.EqualTo(5f));
                Assert.That(result[1].outTangent, Is.EqualTo(6f));
                Assert.That(result[1].inWeight, Is.EqualTo(0.4f));
                Assert.That(result[1].outWeight, Is.EqualTo(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GenericRecorder_LoopEndpointAndSettingsAreCommitted(
            bool legacy)
        {
            GameObject root = new GameObject("Integrated Generic Recorder");
            AnimationClip clip = new AnimationClip();
            try
            {
                RagdollGenericBaker baker =
                    root.AddComponent<RagdollGenericBaker>();
                baker.root = root.transform;
                baker.bakePositionList = new[] { root.transform };
                baker.markAsLegacy = legacy;
                baker.loop = true;
                baker.clipSettingsPolicy =
                    RagdollBakerClipSettingsPolicy.PreserveDestination;
                AnimationClipSettings authored =
                    AnimationUtility.GetAnimationClipSettings(clip);
                authored.cycleOffset = 0.42f;
                AnimationUtility.SetAnimationClipSettings(clip, authored);
                using (RagdollBakerSessionManager.GenericClipRecorder recorder =
                    new RagdollBakerSessionManager.GenericClipRecorder(baker))
                {
                    root.transform.localPosition = Vector3.right * 2f;
                    recorder.Sample(0f);
                    root.transform.localPosition = Vector3.right * 9f;
                    recorder.Sample(0.5f);
                    recorder.Save(clip);
                }
                RagdollBakerLoopUtility.MatchEndKeysToStart(clip);
                RagdollBakerClipSettingsUtility.Apply(baker, null, clip);

                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                    string.Empty, typeof(Transform), "m_LocalPosition.x");
                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    clip, binding);
                Assert.That(curve, Is.Not.Null);
                Assert.That(curve.length, Is.GreaterThanOrEqualTo(2));
                Assert.That(curve.keys[curve.length - 1].time,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(curve.keys[curve.length - 1].value,
                    Is.EqualTo(curve.keys[0].value).Within(0.0001f));
                AnimationClipSettings result =
                    AnimationUtility.GetAnimationClipSettings(clip);
                Assert.That(result.cycleOffset, Is.EqualTo(0.42f));
                Assert.That(result.loopTime, Is.True);
                Assert.That(clip.legacy, Is.EqualTo(legacy));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MultiSegmentCommitFailure_RestoresExistingAndDeletesNewAsset()
        {
            const string folder = "Assets/__HairibarBakerTransactionTests";
            const string existingPath = folder + "/Existing.anim";
            const string newPath = folder + "/New.anim";
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "__HairibarBakerTransactionTests");

            AnimationClip existing = new AnimationClip { name = "Existing" };
            AnimationClip replacement = new AnimationClip { name = "Replacement" };
            AnimationClip addition = new AnimationClip { name = "Addition" };
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                string.Empty, typeof(Transform), "m_LocalPosition.x");
            try
            {
                AnimationUtility.SetEditorCurve(
                    existing, binding, AnimationCurve.Constant(0f, 1f, 2f));
                AnimationClipSettings settings =
                    AnimationUtility.GetAnimationClipSettings(existing);
                settings.cycleOffset = 0.37f;
                AnimationUtility.SetAnimationClipSettings(existing, settings);
                AssetDatabase.CreateAsset(existing, existingPath);
                AssetDatabase.SaveAssets();
                AnimationClip originalReference =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(existingPath);

                AnimationUtility.SetEditorCurve(
                    replacement, binding, AnimationCurve.Constant(0f, 1f, 9f));
                AnimationUtility.SetEditorCurve(
                    addition, binding, AnimationCurve.Constant(0f, 1f, 4f));

                InvalidOperationException thrown =
                    Assert.Throws<InvalidOperationException>(() =>
                        RagdollBakerSessionManager.CommitClipsAtomically(
                            new[] { replacement, addition },
                            new[] { existingPath, newPath },
                            index =>
                            {
                                if (index == 1)
                                    throw new InvalidOperationException(
                                        "Synthetic commit boundary failure.");
                            }));
                Assert.That(thrown.Message, Does.Contain("Synthetic"));

                AnimationClip restored =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(existingPath);
                Assert.That(restored, Is.SameAs(originalReference),
                    "Rollback must preserve the destination asset identity.");
                AnimationCurve restoredCurve =
                    AnimationUtility.GetEditorCurve(restored, binding);
                Assert.That(restoredCurve.Evaluate(0.5f), Is.EqualTo(2f));
                Assert.That(
                    AnimationUtility.GetAnimationClipSettings(restored).cycleOffset,
                    Is.EqualTo(0.37f));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath),
                    Is.Null,
                    "An asset created by the failed transaction must be removed.");
            }
            finally
            {
                if (replacement) Object.DestroyImmediate(replacement);
                if (addition) Object.DestroyImmediate(addition);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
