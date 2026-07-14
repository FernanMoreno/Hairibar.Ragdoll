using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollDefinitionBindingsPlayModeTests
    {
        [UnityTest]
        public IEnumerator RegistryLookups_WorkAfterEnteringPlayMode()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            {
                yield return null;

                RagdollBoneHandle handle;
                Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildBody, out handle), Is.True);
                Assert.That(rig.Bindings.GetBone(handle).Name, Is.EqualTo(rig.ChildName));
                Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildCollider, out handle), Is.True);
                Assert.That(rig.Bindings.GetBone(handle).Name, Is.EqualTo(rig.ChildName));
            }
        }
    }
}
