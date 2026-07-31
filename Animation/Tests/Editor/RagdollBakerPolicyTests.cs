using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
    }
}
