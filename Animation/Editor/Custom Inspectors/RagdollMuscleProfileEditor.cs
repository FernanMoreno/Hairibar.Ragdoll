using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollMuscleProfile))]
    internal sealed class RagdollMuscleProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollMuscleProfile profile = (RagdollMuscleProfile) target;
            EditorGUILayout.Space();

            if (GUILayout.Button("Synchronize Bone Assignments"))
            {
                Undo.RecordObject(profile, "Synchronize muscle groups");
                string synchronizeError;
                if (!profile.TrySynchronizeAssignments(out synchronizeError))
                {
                    Debug.LogError(synchronizeError, profile);
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                }
            }

            if (GUILayout.Button("Synchronize Internal Collision Ignores"))
            {
                Undo.RecordObject(
                    profile,
                    "Synchronize internal collision ignores");
                string synchronizeError;
                if (!profile.TrySynchronizeInternalCollisionIgnores(
                    out synchronizeError))
                {
                    Debug.LogError(synchronizeError, profile);
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                }
            }

            string validationError;
            bool valid = profile.TryValidate(out validationError);
            EditorGUILayout.HelpBox(
                valid
                    ? "The profile contains one semantic group per definition bone and valid symmetric internal-collision ignore rules."
                    : validationError,
                valid ? MessageType.Info : MessageType.Warning);
        }
    }
}
