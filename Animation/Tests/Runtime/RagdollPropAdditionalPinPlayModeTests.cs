using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropAdditionalPinPlayModeTests
    {
        [UnityTest]
        public IEnumerator OffsetPin_AppliesLinearAndAngularMotion()
        {
            GameObject bodyObject = new GameObject("Additional Pin PlayMode Body");
            GameObject targetObject = new GameObject("Additional Pin PlayMode Target");
            try
            {
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.mass = 1f;
                targetObject.transform.position = Vector3.up;

                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep step;
                string error;
                Assert.That(solver.TryApply(
                    body,
                    targetObject.transform,
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        Vector3.right,
                        1f,
                        1f),
                    1f,
                    Time.fixedDeltaTime,
                    out step,
                    out error), Is.True, error);
                Assert.That(step.Applied, Is.True);

                yield return new WaitForFixedUpdate();

                Assert.That(body.velocity.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(body.angularVelocity.sqrMagnitude, Is.GreaterThan(0f));
            }
            finally
            {
                Object.Destroy(bodyObject);
                Object.Destroy(targetObject);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedSolverReset_DoesNotAccumulateLocalOffset()
        {
            GameObject bodyObject = new GameObject("Additional Pin Reset Body");
            GameObject targetObject = new GameObject("Additional Pin Reset Target");
            try
            {
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;
                Vector3 offset = new Vector3(0.4f, -0.2f, 0.7f);
                RagdollPropAdditionalPinSnapshot settings =
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        offset,
                        1f,
                        1f);
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();

                for (int iteration = 0; iteration < 20; iteration++)
                {
                    solver.Reset();
                    RagdollPropAdditionalPinStep step;
                    string error;
                    Assert.That(solver.TryApply(
                        body,
                        targetObject.transform,
                        settings,
                        1f,
                        Time.fixedDeltaTime,
                        out step,
                        out error), Is.True, error);
                    Assert.That(step.TargetPoint,
                        Is.EqualTo(targetObject.transform.TransformPoint(offset)));
                    Assert.That(step.PhysicalPoint,
                        Is.EqualTo(body.transform.TransformPoint(offset)));
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(bodyObject);
                Object.Destroy(targetObject);
            }
        }
    }
}
