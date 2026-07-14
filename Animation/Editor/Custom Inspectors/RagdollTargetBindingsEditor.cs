using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollTargetBindings))]
    internal sealed class RagdollTargetBindingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RagdollTargetBindings targetBindings = (RagdollTargetBindings) target;

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollBindings"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("bindings"),
                new GUIContent("Target Bone Bindings"),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(targetBindings, "Invalidate target binding offsets");
                targetBindings.InvalidateCapturedOffsets();
                EditorUtility.SetDirty(targetBindings);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            if (GUILayout.Button("Auto Bind Unique Names"))
            {
                ExecuteBindingOperation(
                    targetBindings,
                    "Auto bind target bones",
                    targetBindings.TryAutoBindByName);
            }

            if (GUILayout.Button("Capture Current Pose Offsets"))
            {
                ExecuteBindingOperation(
                    targetBindings,
                    "Capture target binding offsets",
                    targetBindings.TryCaptureOffsets);
            }

            EditorGUI.EndDisabledGroup();

            string validationError;
            MessageType messageType = targetBindings.TryValidate(out validationError)
                ? MessageType.Info
                : MessageType.Warning;

            EditorGUILayout.HelpBox(
                messageType == MessageType.Info
                    ? "All target bones are explicitly bound and their offsets are captured."
                    : validationError,
                messageType);
        }

        delegate bool BindingOperation(out string error);

        static void ExecuteBindingOperation(
            RagdollTargetBindings targetBindings,
            string undoName,
            BindingOperation operation)
        {
            Undo.RecordObject(targetBindings, undoName);

            string error;
            if (!operation(out error))
            {
                Debug.LogError(error, targetBindings);
                return;
            }

            EditorUtility.SetDirty(targetBindings);
        }
    }
}
