using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Stable PlayMode anchor for J03. The strict closure validator additionally
    /// requires the complete PlayMode XML root to contain no failed, skipped or
    /// inconclusive cases; this test cannot certify its enclosing run by itself.
    /// </summary>
    public sealed class RagdollPlayModeClosureEvidenceTests
    {
        [UnityTest]
        public IEnumerator J03_PlayModeRunExecutesInitializedRuntimeAssembly()
        {
            RagdollAnimationProfile profile =
                ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            try
            {
                yield return null;
                BoneProfile bone = profile.GetBoneProfile(
                    new BoneName("J03 Runtime Evidence"), true);
                Assert.That(float.IsNaN(bone.positionAlpha), Is.False);
                Assert.That(float.IsNaN(bone.rotationAlpha), Is.False);
                Assert.That(bone.PositionPinWeight,
                    Is.InRange(0f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
