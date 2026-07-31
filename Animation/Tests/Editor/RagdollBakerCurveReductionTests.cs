using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public class RagdollBakerCurveReductionTests
    {
        [Test]
        public void Reduce_RemovesLinearInteriorKeysWithinError()
        {
            List<Keyframe> keys = new List<Keyframe>
            {
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.5f),
                new Keyframe(1f, 1f)
            };

            Keyframe[] reduced = RagdollBakerCurveReduction.Reduce(keys, 0.0001f);

            Assert.That(reduced.Length, Is.EqualTo(2));
            Assert.That(reduced[0].value, Is.Zero);
            Assert.That(reduced[1].value, Is.EqualTo(1f));
        }

        [Test]
        public void Reduce_PreservesKeysOutsideError()
        {
            List<Keyframe> keys = new List<Keyframe>
            {
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f)
            };

            Keyframe[] reduced = RagdollBakerCurveReduction.Reduce(keys, 0.1f);

            Assert.That(reduced.Length, Is.EqualTo(3));
        }
    }
}
