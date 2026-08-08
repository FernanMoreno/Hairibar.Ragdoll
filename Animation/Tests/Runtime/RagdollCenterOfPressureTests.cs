using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollCenterOfPressureTests
    {
        [Test]
        public void PressureUsesImpulseWeightedContacts()
        {
            Vector3 weighted = Vector3.zero;
            float total = 0f;
            int count = 0;
            RagdollCenterOfPressureMath.Accumulate(
                Vector3.zero,
                1f,
                ref weighted,
                ref total,
                ref count);
            RagdollCenterOfPressureMath.Accumulate(
                new Vector3(4f, 0f, 0f),
                3f,
                ref weighted,
                ref total,
                ref count);

            Vector3 pressure;
            Assert.That(
                RagdollCenterOfPressureMath.Resolve(
                    weighted,
                    total,
                    count,
                    out pressure),
                Is.True);
            Assert.That(pressure, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void GroundContactFilteringPreservesPerContactImpulseDistribution()
        {
            Vector3 weighted = Vector3.zero;
            float total = 0f;
            int count = 0;
            float minimumDot = Mathf.Cos(60f * Mathf.Deg2Rad);

            Assert.That(RagdollGroundProbe.AccumulateGroundContact(
                Vector3.zero, Vector3.up, 1f, Vector3.up, minimumDot,
                ref weighted, ref total, ref count), Is.True);
            Assert.That(RagdollGroundProbe.AccumulateGroundContact(
                new Vector3(4f, 0f, 0f), Vector3.up, 3f,
                Vector3.up, minimumDot,
                ref weighted, ref total, ref count), Is.True);
            Assert.That(RagdollGroundProbe.AccumulateGroundContact(
                Vector3.right * 100f, Vector3.right, 100f,
                Vector3.up, minimumDot,
                ref weighted, ref total, ref count), Is.False,
                "Wall contacts must not contribute ground pressure.");

            Vector3 pressure;
            Assert.That(RagdollCenterOfPressureMath.Resolve(
                weighted, total, count, out pressure), Is.True);
            Assert.That(pressure, Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotResolvesComVectorDirectionDistanceAndArbitraryUpAngle()
        {
            RagdollGroundingSnapshot snapshot = new RagdollGroundingSnapshot(
                true,
                0.2f,
                Vector3.zero,
                Vector3.forward,
                new Vector3(0f, 0f, 2f),
                Vector3.zero,
                70f,
                true,
                Vector3.zero,
                Vector3.forward);

            Assert.That(snapshot.HasCenterOfPressure, Is.True);
            Assert.That(snapshot.CenterOfMassVector, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(snapshot.CenterOfMassDirection, Is.EqualTo(Vector3.forward));
            Assert.That(snapshot.CenterOfMassDistance, Is.EqualTo(2f));
            Assert.That(snapshot.CenterOfMassAngle, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void EmptyPressureProducesFiniteNeutralValues()
        {
            RagdollGroundingSnapshot snapshot = RagdollGroundingSnapshot.Empty;

            Assert.That(snapshot.HasCenterOfPressure, Is.False);
            Assert.That(snapshot.CenterOfPressure, Is.EqualTo(Vector3.zero));
            Assert.That(snapshot.CenterOfMassDirection, Is.EqualTo(Vector3.zero));
            Assert.That(snapshot.CenterOfMassDistance, Is.Zero);
            Assert.That(snapshot.CenterOfMassAngle, Is.Zero);
        }
    }
}
