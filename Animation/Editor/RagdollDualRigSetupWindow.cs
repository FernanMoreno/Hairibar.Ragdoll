using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    public sealed class RagdollDualRigSetupWindow : EditorWindow
    {
        Transform targetRoot;
        RagdollDefinitionBindings puppet;
        RagdollAnimationProfile animationProfile;
        int targetLayer;
        int puppetLayer;

        [MenuItem("Tools/Hairibar.Ragdoll/Dual Rig Layer Setup")]
        static void Open()
        {
            GetWindow<RagdollDualRigSetupWindow>(true, "Dual Rig Setup");
        }

        void OnGUI()
        {
            targetRoot = (Transform)EditorGUILayout.ObjectField(
                "Target", targetRoot, typeof(Transform), true);
            puppet = (RagdollDefinitionBindings)EditorGUILayout.ObjectField(
                "Puppet", puppet, typeof(RagdollDefinitionBindings), true);
            animationProfile = (RagdollAnimationProfile)EditorGUILayout.ObjectField(
                "Animation Profile",
                animationProfile,
                typeof(RagdollAnimationProfile),
                false);
            targetLayer = EditorGUILayout.LayerField("Target Layer", targetLayer);
            puppetLayer = EditorGUILayout.LayerField("Puppet Layer", puppetLayer);

            EditorGUILayout.HelpBox(
                "Creates the runtime controllers and Character Behaviours object, "
                + "binds Target/Puppet, assigns layers recursively, and disables "
                + "Target/Puppet collisions. The operation is one Undo transaction.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(
                !targetRoot || !puppet || !animationProfile
                || targetLayer == puppetLayer))
            {
                if (GUILayout.Button("Create Complete Setup")) Apply();
            }
        }

        void Apply()
        {
            RagdollSetupResult result = ApplyCompleteSetup(
                targetRoot,
                puppet,
                animationProfile,
                targetLayer,
                puppetLayer);
            if (result.Succeeded)
            {
                Selection.activeObject = result.Animator;
                EditorGUIUtility.PingObject(result.Animator);
                UnityEngine.Debug.Log("Complete dual-rig setup created.", result.Animator);
            }
            else
            {
                UnityEngine.Debug.LogError(result.Error, puppet);
            }
        }

        internal static RagdollSetupResult ApplyCompleteSetup(
            Transform target,
            RagdollDefinitionBindings puppetBindings,
            RagdollAnimationProfile profile,
            int targetLayerIndex,
            int puppetLayerIndex)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Complete Ragdoll Setup");
            if (target)
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    target.gameObject,
                    "Record Target hierarchy");
            }
            if (puppetBindings)
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    puppetBindings.transform.root.gameObject,
                    "Record Puppet hierarchy");
            }

            if (target && puppetBindings
                && !HaveCommonAncestor(target, puppetBindings.transform))
            {
                Transform targetHierarchyRoot = target.root;
                Transform puppetHierarchyRoot = puppetBindings.transform.root;
                GameObject container = UndoObjectFactory.Instance.CreateGameObject(
                    targetHierarchyRoot.name + " Ragdoll");
                Undo.SetTransformParent(
                    targetHierarchyRoot,
                    container.transform,
                    "Parent Target hierarchy");
                Undo.SetTransformParent(
                    puppetHierarchyRoot,
                    container.transform,
                    "Parent Puppet hierarchy");
            }

            // Record after structural Undo commands: those commands flush pending
            // RecordObjects snapshots before Physics.IgnoreLayerCollision mutates the
            // project setting.
            Object[] physicsSettings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/DynamicsManager.asset");
            if (physicsSettings.Length > 0)
            {
                Undo.RecordObjects(physicsSettings, "Record collision matrix");
            }

            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    target,
                    puppetBindings,
                    profile,
                    targetLayerIndex,
                    puppetLayerIndex,
                    UndoObjectFactory.Instance);
            if (!result.Succeeded)
            {
                Undo.RevertAllDownToGroup(group);
                return result;
            }

            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return result;
        }

        static bool HaveCommonAncestor(Transform first, Transform second)
        {
            if (!first || !second) return false;
            return first.root == second.root;
        }

        sealed class UndoObjectFactory : RagdollRuntimeSetupService.IObjectFactory
        {
            internal static readonly UndoObjectFactory Instance =
                new UndoObjectFactory();

            public T AddComponent<T>(GameObject owner) where T : Component
            {
                return Undo.AddComponent<T>(owner);
            }

            public GameObject CreateGameObject(string name)
            {
                GameObject value = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(value, "Create " + name);
                return value;
            }

            public void Destroy(Object value)
            {
                if (value) Undo.DestroyObjectImmediate(value);
            }
        }
    }
}
