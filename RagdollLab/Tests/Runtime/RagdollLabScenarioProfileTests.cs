using NUnit.Framework;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabScenarioProfileTests
    {
        [TestCase("Idle", "Idle")]
        [TestCase("push", "Push")]
        [TestCase("RecoverablePush", "Push")]
        [TestCase("GetUp", "GetUp")]
        [TestCase("Locomotion", "Locomotion")]
        [TestCase("StaggerRecovery", "Stagger")]
        [TestCase("BalancerOn", "Balancer")]
        public void ResolveRecognizesSupportedScenarioAliasesCaseInsensitively(string input, string expectedId)
        {
            ScenarioProfile profile = RagdollLabScenarioProfiles.Resolve(input);

            Assert.That(profile.available, Is.True);
            Assert.That(profile.id, Is.EqualTo(expectedId));
        }

        [Test]
        public void UnknownOrMissingScenarioFailsClosedToUnavailable()
        {
            ScenarioProfile unknown = RagdollLabScenarioProfiles.Resolve("SomethingElse");
            ScenarioProfile missing = RagdollLabScenarioProfiles.Resolve(null);

            Assert.That(unknown.available, Is.False);
            Assert.That(unknown.id, Is.EqualTo(RagdollLabScenarioProfiles.UnavailableId));
            Assert.That(missing.available, Is.False);
            Assert.That(missing.id, Is.EqualTo(RagdollLabScenarioProfiles.UnavailableId));
        }

        [Test]
        public void MotionBearingProfilesDoNotUseIdleComSpeedDirection()
        {
            ScenarioProfile idle = RagdollLabScenarioProfiles.Resolve("Idle");
            ScenarioProfile locomotion = RagdollLabScenarioProfiles.Resolve("Locomotion");
            ScenarioProfile getUp = RagdollLabScenarioProfiles.Resolve("GetUp");

            Assert.That(idle.ExpectationFor("CenterOfMassSpeed.mean"), Is.EqualTo("lower"));
            Assert.That(locomotion.ExpectationFor("CenterOfMassSpeed.mean"), Is.EqualTo("neutral"));
            Assert.That(getUp.ExpectationFor("CenterOfMassSpeed.mean"), Is.EqualTo("neutral"));
        }

        [Test]
        public void EverySupportedProfileDeclaresRequiredSignals()
        {
            string[] names = { "Idle", "Push", "GetUp", "Locomotion", "Stagger", "Balancer" };
            for (int i = 0; i < names.Length; i++)
            {
                ScenarioProfile profile = RagdollLabScenarioProfiles.Resolve(names[i]);
                Assert.That(profile.requiredSignals, Is.Not.Null.And.Not.Empty, names[i]);
            }
        }
    }
}
