using NUnit.Framework;
using System.Linq;
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
            Assert.That(author.AuthoredRig, Is.SameAs(original));
            Assert.That(references.head.GetComponent<Rigidbody>(),
                Is.SameAs(originalHeadBody));
            Assert.That(foreign, Is.Not.Null);
            float total = 0f;
            foreach (Rigidbody body in author.AuthoredRig.Rigidbodies) total += body.mass;
            Assert.That(total, Is.EqualTo(42f).Within(0.001f));
        }

        [Test]
        public void ConfigurationHash_DetectsScriptEditsAndAppliedState()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);
            string initial = RagdollLiveAuthoringHashUtility.Compute(author);
            author.MarkConfigurationApplied(initial);
            Assert.That(RagdollLiveAuthoringHashUtility.Compute(author),
                Is.EqualTo(author.AppliedConfigurationHash));

            SerializedObject serialized = new SerializedObject(author);
            serialized.FindProperty("options")
                .FindPropertyRelative("totalMass").floatValue = 81f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(RagdollLiveAuthoringHashUtility.Compute(author),
                Is.Not.EqualTo(author.AppliedConfigurationHash));
            Assert.That(author.TryRebuild(out error), Is.True, error);
            author.MarkConfigurationApplied(
                RagdollLiveAuthoringHashUtility.Compute(author));
            Assert.That(RagdollLiveAuthoringHashUtility.Compute(author),
                Is.EqualTo(author.AppliedConfigurationHash));
        }

        [Test]
        public void StableHash_SurvivesPrefabUnloadAndReload()
        {
            const string Path = "Assets/__HairibarAuthoringHashTest.prefab";
            try
            {
                RagdollBipedReferences references = CreateReferences();
                RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
                AssignReferences(author, references);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
                Object.DestroyImmediate(root);
                root = null;

                GameObject firstRoot = PrefabUtility.LoadPrefabContents(Path);
                string first;
                try
                {
                    first = RagdollLiveAuthoringHashUtility.Compute(
                        firstRoot.GetComponent<RagdollLiveAuthoring>());
                }
                finally { PrefabUtility.UnloadPrefabContents(firstRoot); }

                AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
                GameObject secondRoot = PrefabUtility.LoadPrefabContents(Path);
                try
                {
                    string second = RagdollLiveAuthoringHashUtility.Compute(
                        secondRoot.GetComponent<RagdollLiveAuthoring>());
                    Assert.That(second, Is.EqualTo(first));
                }
                finally { PrefabUtility.UnloadPrefabContents(secondRoot); }
            }
            finally { AssetDatabase.DeleteAsset(Path); }
        }

        [Test]
        public void ReplacementPreflight_PreservesRigWhenForeignPhysicsAppears()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);
            RagdollAuthoredRig original = author.AuthoredRig;
            SphereCollider foreign = references.head.gameObject
                .AddComponent<SphereCollider>();

            Assert.That(author.TryRebuild(out error), Is.False);
            Assert.That(error, Does.Contain("not owned"));
            Assert.That(author.AuthoredRig, Is.SameAs(original));
            Assert.That(foreign, Is.Not.Null);
            Assert.That(original.Rigidbodies.All(body => body), Is.True);
        }

        [Test]
        public void StagedRebuild_ExceptionRestoresExactOwnedComponents()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);
            RagdollAuthoredRig rig = author.AuthoredRig;
            Rigidbody[] bodies = (Rigidbody[])rig.Rigidbodies.Clone();
            Collider[] colliders = (Collider[])rig.Colliders.Clone();
            ConfigurableJoint[] joints =
                (ConfigurableJoint[])rig.Joints.Clone();
            BoxCollider hips = colliders[0] as BoxCollider;
            Vector3 hipsCenter = hips.center;
            Vector3 hipsSize = hips.size;
            RagdollAuthoringOptions changed = author.Options;
            changed.totalMass = 99f;
            changed.headCollider = RagdollAuthoringColliderShape.Sphere;

            Assert.That(RagdollRuntimeAuthoring.TryRebuild(
                rig,
                references,
                changed,
                new ThrowOnSphereFactory(),
                out error), Is.False);

            Assert.That(error, Does.Contain("Synthetic"));
            Assert.That(rig.Rigidbodies, Is.EqualTo(bodies));
            Assert.That(rig.Colliders, Is.EqualTo(colliders));
            Assert.That(rig.Joints, Is.EqualTo(joints));
            Assert.That(hips.center, Is.EqualTo(hipsCenter));
            Assert.That(hips.size, Is.EqualTo(hipsSize));
            Assert.That(bodies.All(body => body), Is.True);
            Assert.That(colliders.All(collider => collider), Is.True);
            Assert.That(joints.All(joint => joint), Is.True);
        }

        [Test]
        public void CommitBoundaryFailure_RestoresRegistryAndAllMasses()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);
            RagdollAuthoredRig rig = author.AuthoredRig;
            Rigidbody[] bodies = (Rigidbody[])rig.Rigidbodies.Clone();
            float[] masses = bodies.Select(body => body.mass).ToArray();
            RagdollAuthoringOptions changed = author.Options;
            changed.totalMass = 123f;

            Assert.That(RagdollRuntimeAuthoring.TryRebuild(
                rig,
                references,
                changed,
                new ThrowAfterCommitFactory(),
                out error), Is.False);

            Assert.That(error, Does.Contain("commit boundary"));
            Assert.That(rig.Rigidbodies, Is.EqualTo(bodies));
            for (int index = 0; index < bodies.Length; index++)
                Assert.That(bodies[index].mass,
                    Is.EqualTo(masses[index]).Within(0.0001f));
        }

        [Test]
        public void RebuildStage_IsCompleteAndInactiveBeforeCommit()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollLiveAuthoring author = root.AddComponent<RagdollLiveAuthoring>();
            AssignReferences(author, references);
            string error;
            Assert.That(author.TryRebuild(out error), Is.True, error);

            GameObject stage;
            Assert.That(author.TryBuildInactiveStage(out stage, out error),
                Is.True, error);
            try
            {
                Assert.That(stage.activeSelf, Is.False);
                RagdollAuthoredRig stagedRig =
                    stage.GetComponentInChildren<RagdollAuthoredRig>(true);
                Assert.That(stagedRig, Is.Not.Null);
                Assert.That(stagedRig.Rigidbodies.Length,
                    Is.EqualTo(author.AuthoredRig.Rigidbodies.Length));
                Assert.That(stagedRig.Rigidbodies.All(body => body), Is.True);
                Assert.That(stagedRig.Colliders.All(collider => collider), Is.True);
                Assert.That(stagedRig.Joints.All(joint => joint), Is.True);
            }
            finally { Object.DestroyImmediate(stage); }
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

    sealed class ThrowOnSphereFactory : RagdollRuntimeAuthoring.IObjectFactory
    {
        public T AddComponent<T>(GameObject owner) where T : Component
        {
            if (typeof(T) == typeof(SphereCollider))
                throw new System.InvalidOperationException(
                    "Synthetic staged authoring failure.");
            return owner.AddComponent<T>();
        }

        public void Destroy(Object value)
        {
            if (value) Object.DestroyImmediate(value);
        }
    }

    sealed class ThrowAfterCommitFactory :
        RagdollRuntimeAuthoring.IObjectFactory,
        RagdollRuntimeAuthoring.ITransactionBoundaryProbe
    {
        public T AddComponent<T>(GameObject owner) where T : Component =>
            owner.AddComponent<T>();
        public void Destroy(Object value)
        {
            if (value) Object.DestroyImmediate(value);
        }
        public void AfterRegistryCommit()
        {
            throw new System.InvalidOperationException(
                "Synthetic commit boundary failure.");
        }
    }

    public sealed class ForeignAuthoringMarker : MonoBehaviour
    {
    }
}
