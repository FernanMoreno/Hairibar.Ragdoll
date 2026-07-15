using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollSimulationModeController))]
    internal sealed class RagdollSimulationModeControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollSimulationModeController controller =
                (RagdollSimulationModeController) target;

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                RagdollAnimator animator = controller.GetComponent<RagdollAnimator>();
                if (animator && animator.Bindings
                    && (controller.transform == animator.Bindings.transform
                        || controller.transform.IsChildOf(animator.Bindings.transform)))
                {
                    EditorGUILayout.HelpBox(
                        "Place this controller on the Target side of the dual rig, outside the Puppet hierarchy. Otherwise Disabled mode could deactivate its own controller.",
                        MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Active restores the authored RagdollPowerProfile. Kinematic keeps the Puppet active and collidable. Disabled deactivates the Puppet hierarchy.",
                        MessageType.Info);
                }

                return;
            }

            EditorGUILayout.HelpBox(
                "Current mode: " + controller.CurrentMode
                + "\nTarget mode: " + controller.TargetMode
                + "\nTransitioning: " + controller.IsTransitioning
                + "\nTransition progress: " + controller.TransitionProgress.ToString("P0")
                + "\nActive drive weight: " + controller.ActiveDriveWeight.ToString("F2"),
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(!controller.IsInitialized);
            if (GUILayout.Button("Active"))
            {
                controller.SetMode(RagdollSimulationMode.Active);
            }

            if (GUILayout.Button("Kinematic"))
            {
                controller.SetMode(RagdollSimulationMode.Kinematic);
            }

            if (GUILayout.Button("Disabled"))
            {
                controller.SetMode(RagdollSimulationMode.Disabled);
            }

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(
                controller.CurrentMode != RagdollSimulationMode.Active
                || controller.IsTransitioning);
            if (GUILayout.Button("Refresh Active Configuration"))
            {
                controller.RefreshActiveConfiguration();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
        }
    }
}
