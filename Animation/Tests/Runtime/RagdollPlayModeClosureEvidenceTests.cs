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
            GameObject owner = new GameObject("J03 Runtime Evidence");
            try
            {
                RagdollCollisionHub hub = owner.AddComponent<RagdollCollisionHub>();
                yield return null;
                Assert.That(hub, Is.Not.Null);
                Assert.That(hub.isActiveAndEnabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
