using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    public sealed class RagdollAuthoringWizard : ScriptableWizard
    {
        public enum ReferenceMode
        {
            HumanoidAvatar,
            ExplicitGeneric
        }

        public ReferenceMode referenceMode;
        public Animator humanoidAnimator;
        public RagdollBipedReferences genericReferences = new RagdollBipedReferences();
        public RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;

        [MenuItem("Tools/Hairibar.Ragdoll/Automatic Biped Authoring")]
        static void Open()
        {
            RagdollAuthoringWizard wizard = DisplayWizard<RagdollAuthoringWizard>(
                "Automatic Biped Ragdoll",
                "Create Ragdoll");
            GameObject selected = Selection.activeGameObject;
            wizard.humanoidAnimator = selected
                ? selected.GetComponentInChildren<Animator>()
                : null;
        }

        void OnEnable()
        {
            helpString = "Humanoid references come from the valid Avatar mapping. "
                + "Generic rigs require explicit semantic references. Creation is fully Undoable.";
        }

        void OnWizardUpdate()
        {
            RagdollBipedReferences references;
            string error;
            if (TryResolveReferences(out references, out error))
            {
                isValid = true;
                errorString = string.Empty;
            }
            else
            {
                isValid = false;
                errorString = error;
            }
        }

        void OnWizardCreate()
        {
            RagdollBipedReferences references;
            string error;
            if (!TryResolveReferences(out references, out error))
            {
                UnityEngine.Debug.LogError(error);
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create automatic biped ragdoll");
            RagdollAuthoredRig rig;
            if (!RagdollRuntimeAuthoring.TryBuild(
                references,
                options,
                out rig,
                out error))
            {
                UnityEngine.Debug.LogError(error, references.hips);
                return;
            }

            RegisterCreated(rig.Rigidbodies);
            RegisterCreated(rig.Colliders);
            RegisterCreated(rig.Joints);
            Undo.RegisterCreatedObjectUndo(rig, "Create authored rig ownership");
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeObject = rig;
            EditorGUIUtility.PingObject(rig);
        }

        bool TryResolveReferences(
            out RagdollBipedReferences references,
            out string error)
        {
            if (referenceMode == ReferenceMode.HumanoidAvatar)
            {
                return RagdollBipedReferences.TryFromHumanoid(
                    humanoidAnimator,
                    out references,
                    out error);
            }

            references = genericReferences;
            if (references == null)
            {
                error = "Explicit generic references are required.";
                return false;
            }
            return references.Validate(out error);
        }

        static void RegisterCreated<T>(T[] values) where T : Object
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index])
                {
                    Undo.RegisterCreatedObjectUndo(values[index], "Create ragdoll component");
                }
            }
        }
    }
}
