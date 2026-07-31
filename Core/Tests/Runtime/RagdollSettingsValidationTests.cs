using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollSettingsValidationTests
    {
        [Test]
        public void InvalidPhysicsScalars_AreSanitizedBeforeApplication()
        {
            GameObject owner = new GameObject("Settings validation");
            owner.SetActive(false);
            try
            {
                RagdollSettings settings = owner.AddComponent<RagdollSettings>();
                settings.limitBounciness = float.NaN;
                settings.limitSpring = float.PositiveInfinity;
                settings.totalMass = 0f;
                settings.drag = -1f;
                settings.maxAngularVelocity = float.NegativeInfinity;
                settings.maximumInertiaTensorRatio = float.NaN;

                MethodInfo sanitize = typeof(RagdollSettings).GetMethod(
                    "SanitizeAuthoredSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(sanitize, Is.Not.Null);
                sanitize.Invoke(settings, null);

                Assert.That(settings.limitBounciness, Is.EqualTo(0.3f));
                Assert.That(settings.limitSpring, Is.EqualTo(1000f));
                Assert.That(settings.totalMass, Is.EqualTo(7f));
                Assert.That(settings.drag, Is.Zero);
                Assert.That(settings.maxAngularVelocity, Is.EqualTo(7f));
                Assert.That(settings.maximumInertiaTensorRatio, Is.EqualTo(10f));
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(owner);
                else Object.DestroyImmediate(owner);
            }
        }
    }
}
