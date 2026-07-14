using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollBehaviourController))]
    internal sealed class RagdollBehaviourControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollBehaviourController controller =
                (RagdollBehaviourController) target;
            Transform root = controller.BehaviourRoot;
            RagdollBehaviourBase[] behaviours = root
                ? root.GetComponentsInChildren<RagdollBehaviourBase>(true)
                : new RagdollBehaviourBase[0];

            EditorGUILayout.Space();
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    controller.ActiveBehaviour
                        ? "Active behaviour: " + controller.ActiveBehaviour.GetType().Name
                        : "No ragdoll behaviour is active.",
                    MessageType.Info);
                return;
            }

            int enabledCount = 0;
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index].enabled
                    && behaviours[index].gameObject.activeSelf)
                {
                    enabledCount++;
                }
            }

            if (behaviours.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No RagdollBehaviourBase components were found under the configured root. "
                    + "The controller will remain neutral.",
                    MessageType.Info);
            }
            else if (enabledCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "All discovered behaviours are disabled. Enable one behaviour to select the initial behaviour.",
                    MessageType.Info);
            }
            else if (enabledCount > 1)
            {
                EditorGUILayout.HelpBox(
                    "More than one behaviour is enabled. At runtime the first discovered behaviour will be activated and all others disabled.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    behaviours.Length + " behaviour component(s) found. One is selected as the initial behaviour.",
                    MessageType.Info);
            }
        }
    }
}
