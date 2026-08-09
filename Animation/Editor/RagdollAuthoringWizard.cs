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
            RagdollAuthoredRig rig;
            string error;
            if (!TryCreateFromSelection(
                Selection.activeGameObject,
                referenceMode,
                humanoidAnimator,
                genericReferences,
                options,
                out rig,
                out error))
            {
                UnityEngine.Debug.LogError(error);
                return;
            }

            Selection.activeObject = rig;
            EditorGUIUtility.PingObject(rig);
        }

        internal static bool TryCreateFromSelection(
            GameObject selected,
            ReferenceMode mode,
            Animator humanoid,
            RagdollBipedReferences generic,
            RagdollAuthoringOptions authoringOptions,
            out RagdollAuthoredRig rig,
            out string error)
        {
            rig = null;
            if (!selected)
            {
                error = "Select the character root before creating a ragdoll.";
                return false;
            }

            RagdollBipedReferences references;
            if (mode == ReferenceMode.HumanoidAvatar)
            {
                if (!humanoid || !humanoid.transform.IsChildOf(selected.transform)
                    && humanoid.gameObject != selected)
                {
                    error = "The Humanoid Animator must belong to the selected character.";
                    return false;
                }
                if (!RagdollBipedReferences.TryFromHumanoid(
                    humanoid, out references, out error))
                    return false;
            }
            else
            {
                references = generic;
                if (references == null || !references.Validate(out error))
                    return false;
                if (references.hips != selected.transform
                    && !references.hips.IsChildOf(selected.transform))
                {
                    error = "The explicit hips reference must belong to the selected character.";
                    return false;
                }
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create automatic biped ragdoll");
            try
            {
                if (!RagdollRuntimeAuthoring.TryBuild(
                    references,
                    authoringOptions,
                    out rig,
                    out error))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                RegisterCreated(rig.Rigidbodies);
                RegisterCreated(rig.Colliders);
                RegisterCreated(rig.Joints);
                Undo.RegisterCreatedObjectUndo(
                    rig, "Create authored rig ownership");
                Undo.CollapseUndoOperations(undoGroup);
                error = string.Empty;
                return true;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                rig = null;
                error = "Automatic ragdoll creation failed: " + exception.Message;
                return false;
            }
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
