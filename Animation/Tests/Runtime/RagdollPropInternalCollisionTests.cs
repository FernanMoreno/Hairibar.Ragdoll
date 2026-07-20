using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropInternalCollisionTests
    {
        [Test]
        public void Settings_MatchAllSpecificMuscleAndSemanticGroup()
        {
            BoneName hand = new BoneName("Hand");
            RagdollPropInternalCollisionSettings settings =
                new RagdollPropInternalCollisionSettings(
                    false,
                    new[] { hand },
                    new[] { RagdollMuscleGroup.Head });

            Assert.That(settings.Matches(hand, RagdollMuscleGroup.Arm), Is.True);
            Assert.That(
                settings.Matches(new BoneName("Skull"), RagdollMuscleGroup.Head),
                Is.True);
            Assert.That(
                settings.Matches(new BoneName("Foot"), RagdollMuscleGroup.Foot),
                Is.False);
            settings.IgnoreAll = true;
            Assert.That(
                settings.Matches(new BoneName("Foot"), RagdollMuscleGroup.Foot),
                Is.True);
        }

        [Test]
        public void Session_ForcesAndRestoresFalseBaseline()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                RagdollPropInternalCollisionSession session = rig.CreateSession(
                    new RagdollPropInternalCollisionSettings(true));
                Assert.That(session.PairCount, Is.EqualTo(1));
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.True);

                session.RequestRelease();
                Assert.That(session.IsReleased, Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.False);
            }
        }

        [Test]
        public void Session_RearmsIgnoreAfterExternalReset()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                RagdollPropInternalCollisionSession session = rig.CreateSession(
                    new RagdollPropInternalCollisionSettings(true));
                Physics.IgnoreCollision(
                    rig.PropCollider,
                    rig.BodyCollider,
                    false);
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.False);

                session.ReapplyForcedIgnores();
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.True);
            }
        }


        [Test]
        public void ResumeForcedIgnores_RearmsRollbackWithoutLosingBaseline()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                RagdollPropInternalCollisionSession session = rig.CreateSession(
                    new RagdollPropInternalCollisionSettings(true));
                session.RequestRelease();
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.False);

                session.ResumeForcedIgnores();
                Assert.That(session.ReleaseRequested, Is.False);
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.True);

                session.RequestRelease();
                Assert.That(session.IsReleased, Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.False);
            }
        }

        [Test]
        public void TrueAuthoredBaseline_WaitsForReactivationAndRestoresTrue()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                Physics.IgnoreCollision(
                    rig.PropCollider,
                    rig.BodyCollider,
                    true);
                RagdollPropInternalCollisionSession session = rig.CreateSession(
                    new RagdollPropInternalCollisionSettings(true));

                rig.Body.SetActive(false);
                session.RequestRelease();
                Assert.That(session.IsReleased, Is.False);

                rig.Body.SetActive(true);
                Assert.That(session.TryRestoreBaselines(), Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(rig.PropCollider, rig.BodyCollider),
                    Is.True);
            }
        }

        [Test]
        public void FalseBaseline_CanReleaseWhilePuppetColliderIsDisabled()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                RagdollPropInternalCollisionSession session = rig.CreateSession(
                    new RagdollPropInternalCollisionSettings(true));
                rig.Body.SetActive(false);
                session.RequestRelease();
                Assert.That(session.IsReleased, Is.True);
            }
        }

        [Test]
        public void Resolution_UsesNamesAndGroupsAndSkipsSlotAndTriggers()
        {
            using (CollisionRig rig = new CollisionRig())
            {
                GameObject ignored = new GameObject("Ignored Target");
                try
                {
                    Rigidbody body = ignored.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    Collider ignoredCollider = ignored.AddComponent<BoxCollider>();
                    Collider trigger = ignored.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;

                    RagdollPropCollisionMuscle[] muscles =
                    {
                        new RagdollPropCollisionMuscle(
                            default(RagdollBoneHandle),
                            new BoneName("Hand"),
                            RagdollMuscleGroup.Hand,
                            new[] { rig.BodyCollider }),
                        new RagdollPropCollisionMuscle(
                            RagdollBoneHandle.Invalid,
                            new BoneName("Slot"),
                            RagdollMuscleGroup.Prop,
                            new[] { ignoredCollider, trigger })
                    };
                    RagdollPropInternalCollisionSession session;
                    string error;
                    Assert.That(
                        RagdollPropInternalCollisionSession.TryCreate(
                            new[] { rig.PropCollider },
                            muscles,
                            RagdollBoneHandle.Invalid,
                            new RagdollPropInternalCollisionSettings(true),
                            out session,
                            out error),
                        Is.True,
                        error);
                    Assert.That(session.PairCount, Is.EqualTo(1));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(ignored);
                }
            }
        }

        sealed class CollisionRig : System.IDisposable
        {
            public GameObject Prop { get; } = new GameObject("Prop Collider");
            public GameObject Body { get; } = new GameObject("Body Collider");
            public Collider PropCollider { get; }
            public Collider BodyCollider { get; }

            public CollisionRig()
            {
                Rigidbody propBody = Prop.AddComponent<Rigidbody>();
                propBody.isKinematic = true;
                PropCollider = Prop.AddComponent<BoxCollider>();
                Rigidbody body = Body.AddComponent<Rigidbody>();
                body.isKinematic = true;
                BodyCollider = Body.AddComponent<CapsuleCollider>();
            }

            public RagdollPropInternalCollisionSession CreateSession(
                RagdollPropInternalCollisionSettings settings)
            {
                RagdollPropInternalCollisionSession session;
                string error;
                Assert.That(
                    RagdollPropInternalCollisionSession.TryCreate(
                        new[] { PropCollider },
                        new[]
                        {
                            new RagdollPropCollisionMuscle(
                                default(RagdollBoneHandle),
                                new BoneName("Body"),
                                RagdollMuscleGroup.Spine,
                                new[] { BodyCollider })
                        },
                        (RagdollBoneHandle?)null,
                        settings,
                        out session,
                        out error),
                    Is.True,
                    error);
                return session;
            }

            public void Dispose()
            {
                RagdollPropTestRig.DestroyObject(Prop);
                RagdollPropTestRig.DestroyObject(Body);
            }
        }
    }
}
