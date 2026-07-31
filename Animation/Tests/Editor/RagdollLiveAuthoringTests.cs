using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollLiveAuthoringTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            Undo.ClearAll();
        }

        [Test]
        public void InspectorRebuild_IsFullyUndoableAndRedoable()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(
                author,
                typeof(RagdollLiveAuthoringEditor));
            try
            {
                MethodInfo rebuild = typeof(RagdollLiveAuthoringEditor).GetMethod(
                    "RebuildWithUndo",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(rebuild, Is.Not.Null);
                rebuild.Invoke(inspector, null);
                Assert.That(author.AuthoredRig, Is.Not.Null);
                Assert.That(references.hips.GetComponent<Rigidbody>(), Is.Not.Null);

                Undo.PerformUndo();
                Assert.That(author.AuthoredRig == null, Is.True);
                Assert.That(references.hips.GetComponent<Rigidbody>(), Is.Null);

                Undo.PerformRedo();
                Assert.That(author.AuthoredRig, Is.Not.Null);
                Assert.That(references.hips.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(author.AuthoredRig.Rigidbodies.Length, Is.EqualTo(16));
                Assert.That(author.AuthoredRig.Colliders.Length, Is.EqualTo(16));
                Assert.That(author.AuthoredRig.Joints.Length, Is.EqualTo(16));
            }
            finally
            {
                Object.DestroyImmediate(inspector);
            }
        }

        [Test]
        public void Rebuild_ValidatesFirstAndPreservesForeignComponents()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            ForeignAuthoringMarker foreign =
                references.head.gameObject.AddComponent<ForeignAuthoringMarker>();
            AssignReferences(author, references);

            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);
            RagdollAuthoredRig original = author.AuthoredRig;
            Rigidbody originalHeadBody = references.head.GetComponent<Rigidbody>();
            Assert.That(original, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(author);
            SerializedProperty referenceProperty = serialized.FindProperty("references");
            referenceProperty.FindPropertyRelative("rightLowerArm").objectReferenceValue =
                references.leftLowerArm;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(author.TryRebuild(out error), Is.False);
            Assert.That(author.AuthoredRig, Is.SameAs(original));
            Assert.That(originalHeadBody, Is.Not.Null);
            Assert.That(foreign, Is.Not.Null);

            serialized = new SerializedObject(author);
            referenceProperty = serialized.FindProperty("references");
            referenceProperty.FindPropertyRelative("rightLowerArm").objectReferenceValue =
                references.rightLowerArm;
            serialized.FindProperty("options")
                .FindPropertyRelative("totalMass").floatValue = 42f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(author.TryRebuild(out error), Is.True, error);
            Assert.That(author.AuthoredRig, Is.Not.SameAs(original));
            Assert.That(originalHeadBody == null, Is.True);
            Assert.That(foreign, Is.Not.Null);
            float total = 0f;
            foreach (Rigidbody body in author.AuthoredRig.Rigidbodies) total += body.mass;
            Assert.That(total, Is.EqualTo(42f).Within(0.001f));
        }

        void AssignReferences(
            RagdollLiveAuthoring author,
            RagdollBipedReferences source)
        {
            SerializedObject serialized = new SerializedObject(author);
            SerializedProperty value = serialized.FindProperty("references");
            Set(value, "hips", source.hips);
            Set(value, "spine", source.spine);
            Set(value, "chest", source.chest);
            Set(value, "head", source.head);
            Set(value, "leftUpperArm", source.leftUpperArm);
            Set(value, "leftLowerArm", source.leftLowerArm);
            Set(value, "leftHand", source.leftHand);
            Set(value, "rightUpperArm", source.rightUpperArm);
            Set(value, "rightLowerArm", source.rightLowerArm);
            Set(value, "rightHand", source.rightHand);
            Set(value, "leftUpperLeg", source.leftUpperLeg);
            Set(value, "leftLowerLeg", source.leftLowerLeg);
            Set(value, "leftFoot", source.leftFoot);
            Set(value, "rightUpperLeg", source.rightUpperLeg);
            Set(value, "rightLowerLeg", source.rightLowerLeg);
            Set(value, "rightFoot", source.rightFoot);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        RagdollBipedReferences CreateReferences()
        {
            root = new GameObject("Biped");
            Transform hips = Bone("Hips", root.transform, Vector3.zero);
            Transform spine = Bone("Spine", hips, Vector3.up * 0.4f);
            Transform chest = Bone("Chest", spine, Vector3.up * 0.4f);
            Transform head = Bone("Head", chest, Vector3.up * 0.5f);
            Transform lua = Bone("LeftUpperArm", chest, new Vector3(-0.3f, 0.2f, 0f));
            Transform lla = Bone("LeftLowerArm", lua, Vector3.left * 0.4f);
            Transform lh = Bone("LeftHand", lla, Vector3.left * 0.3f);
            Transform rua = Bone("RightUpperArm", chest, new Vector3(0.3f, 0.2f, 0f));
            Transform rla = Bone("RightLowerArm", rua, Vector3.right * 0.4f);
            Transform rh = Bone("RightHand", rla, Vector3.right * 0.3f);
            Transform lul = Bone("LeftUpperLeg", hips, new Vector3(-0.2f, -0.4f, 0f));
            Transform lll = Bone("LeftLowerLeg", lul, Vector3.down * 0.5f);
            Transform lf = Bone("LeftFoot", lll, new Vector3(0f, -0.4f, 0.15f));
            Transform rul = Bone("RightUpperLeg", hips, new Vector3(0.2f, -0.4f, 0f));
            Transform rll = Bone("RightLowerLeg", rul, Vector3.down * 0.5f);
            Transform rf = Bone("RightFoot", rll, new Vector3(0f, -0.4f, 0.15f));
            return new RagdollBipedReferences
            {
                hips = hips, spine = spine, chest = chest, head = head,
                leftUpperArm = lua, leftLowerArm = lla, leftHand = lh,
                rightUpperArm = rua, rightLowerArm = rla, rightHand = rh,
                leftUpperLeg = lul, leftLowerLeg = lll, leftFoot = lf,
                rightUpperLeg = rul, rightLowerLeg = rll, rightFoot = rf
            };
        }

        static void Set(SerializedProperty parent, string name, Object value)
        {
            parent.FindPropertyRelative(name).objectReferenceValue = value;
        }

        static Transform Bone(string name, Transform parent, Vector3 position)
        {
            Transform value = new GameObject(name).transform;
            value.SetParent(parent, false);
            value.localPosition = position;
            return value;
        }
    }

    public sealed class ForeignAuthoringMarker : MonoBehaviour
    {
    }
}
