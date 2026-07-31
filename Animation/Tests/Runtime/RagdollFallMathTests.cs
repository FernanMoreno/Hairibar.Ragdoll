using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollFallMathTests
    {
        [Test]
        public void WritheBlend_UsesLargestDocumentedSignal()
        {
            Assert.That(
                RagdollFallMath.ResolveWritheBlend(2f, 0.25f, 4f, 1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                RagdollFallMath.ResolveWritheBlend(1f, 0.75f, 4f, 1f),
                Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void WritheBlend_ClampsAndIgnoresDownwardVelocity()
        {
            Assert.That(
                RagdollFallMath.ResolveWritheBlend(8f, -10f, 4f, 1f),
                Is.EqualTo(1f));
            Assert.That(
                RagdollFallMath.ResolveWritheBlend(0f, -10f, 4f, 1f),
                Is.EqualTo(0f));
        }

        [Test]
        public void BlendMovement_IsRateLimited()
        {
            Assert.That(
                RagdollFallMath.MoveBlend(0.2f, 1f, 3f, 0.1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                RagdollFallMath.MoveBlend(0.8f, 0f, 2f, 0.1f),
                Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void EndRequiresAllConditionsAndStrictVelocityThreshold()
        {
            Assert.That(
                RagdollFallMath.CanEnd(true, false, 1.5f, 1.5f, 0.49f, 0.5f),
                Is.True);
            Assert.That(
                RagdollFallMath.CanEnd(true, false, 1.5f, 1.5f, 0.5f, 0.5f),
                Is.False);
            Assert.That(
                RagdollFallMath.CanEnd(false, false, 10f, 1.5f, 0f, 0.5f),
                Is.False);
            Assert.That(
                RagdollFallMath.CanEnd(true, true, 10f, 1.5f, 0f, 0.5f),
                Is.False);
        }

        [Test]
        public void UpDirection_SupportsArbitraryAndZeroGravity()
        {
            Assert.That(
                RagdollFallMath.ResolveUp(new Vector3(9.81f, 0f, 0f)),
                Is.EqualTo(Vector3.left));
            Assert.That(
                RagdollFallMath.ResolveUp(Vector3.zero),
                Is.EqualTo(Vector3.up));
        }

        [Test]
        public void BehaviourDefaults_MatchOfficialDocumentedSurface()
        {
            GameObject gameObject = new GameObject("Fall defaults");
            try
            {
                RagdollFallBehaviour behaviour =
                    gameObject.AddComponent<RagdollFallBehaviour>();

                Assert.That(behaviour.StateName, Is.EqualTo("Falling"));
                Assert.That(behaviour.TransitionDuration, Is.EqualTo(0.4f));
                Assert.That(behaviour.Layer, Is.EqualTo(-1));
                Assert.That(behaviour.FixedTime, Is.EqualTo(float.NegativeInfinity));
                Assert.That(behaviour.BlendParameter, Is.EqualTo("FallBlend"));
                Assert.That(behaviour.WritheHeight, Is.EqualTo(4f));
                Assert.That(behaviour.WritheVerticalVelocity, Is.EqualTo(1f));
                Assert.That(behaviour.BlendSpeed, Is.EqualTo(3f));
                Assert.That(behaviour.BlendMappingSpeed, Is.EqualTo(1f));
                Assert.That(behaviour.CanEnd, Is.False);
                Assert.That(behaviour.MinimumTime, Is.EqualTo(1.5f));
                Assert.That(behaviour.MaximumEndVelocity, Is.EqualTo(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeProperties_SanitizeAndApplyImmediately()
        {
            GameObject gameObject = new GameObject("Fall runtime settings");
            try
            {
                RagdollFallBehaviour behaviour =
                    gameObject.AddComponent<RagdollFallBehaviour>();
                behaviour.StateName = null;
                behaviour.TransitionDuration = float.NaN;
                behaviour.Layer = -8;
                behaviour.FixedTime = float.PositiveInfinity;
                behaviour.BlendParameter = "RuntimeBlend";
                behaviour.RaycastLayers = 1 << 7;
                behaviour.WritheHeight = -1f;
                behaviour.WritheYVelocity = float.NaN;
                behaviour.BlendSpeed = -2f;
                behaviour.BlendMappingSpeed = float.PositiveInfinity;
                behaviour.CanEnd = true;
                behaviour.MinimumTime = -5f;
                behaviour.MaximumEndVelocity = float.NaN;

                Assert.That(behaviour.StateName, Is.Empty);
                Assert.That(behaviour.TransitionDuration, Is.Zero);
                Assert.That(behaviour.Layer, Is.EqualTo(-1));
                Assert.That(behaviour.FixedTime, Is.EqualTo(float.NegativeInfinity));
                Assert.That(behaviour.BlendParameter, Is.EqualTo("RuntimeBlend"));
                Assert.That(behaviour.RaycastLayers.value, Is.EqualTo(1 << 7));
                Assert.That(behaviour.WritheHeight, Is.Zero);
                Assert.That(behaviour.WritheYVelocity, Is.Zero);
                Assert.That(behaviour.BlendSpeed, Is.Zero);
                Assert.That(behaviour.BlendMappingSpeed, Is.Zero);
                Assert.That(behaviour.CanEnd, Is.True);
                Assert.That(behaviour.MinimumTime, Is.Zero);
                Assert.That(behaviour.MaximumEndVelocity, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
