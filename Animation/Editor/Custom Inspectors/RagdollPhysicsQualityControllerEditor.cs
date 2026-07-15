using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollPhysicsQualityController))]
    internal sealed class RagdollPhysicsQualityControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollPhysicsQualityController controller =
                (RagdollPhysicsQualityController) target;
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                if (!controller.Profile)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a RagdollPhysicsQualityProfile.",
                        MessageType.Error);
                }
                else if (controller.AutomaticDistance && !controller.Observer)
                {
                    EditorGUILayout.HelpBox(
                        "No explicit observer is assigned. The controller will cache Camera.main when it initializes.",
                        MessageType.Info);
                }

                EditorGUILayout.HelpBox(
                    "While enabled, this component owns the global simulation mode. Route gameplay quality requests through SetManualLevel or ResumeAutomaticDistance.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Requested level: " + controller.RequestedLevel
                + "\nApplied level: " + controller.AppliedLevel
                + "\nDistance squared: " + controller.DistanceSquared.ToString("F2")
                + "\nBudget approved: " + controller.BudgetApproved,
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(!controller.IsInitialized);
            if (GUILayout.Button("Refresh Now")) controller.RefreshNow();
            if (GUILayout.Button("Resume Automatic Distance"))
            {
                controller.ResumeAutomaticDistance();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
