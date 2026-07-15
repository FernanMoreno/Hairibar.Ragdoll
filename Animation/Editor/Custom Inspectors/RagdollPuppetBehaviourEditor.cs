using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollPuppetBehaviour))]
    internal sealed class RagdollPuppetBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollPuppetBehaviour behaviour =
                (RagdollPuppetBehaviour) target;

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Automatic GetUp ground validation and prone/supine alignment are intentionally reserved for the next behaviour module.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "State: " + behaviour.State
                + "\nState time: " + behaviour.StateElapsedTime.ToString("F2")
                + "\nGetUp progress: " + behaviour.GetUpProgress.ToString("P0")
                + "\nReady to get up: " + behaviour.CanBeginGetUp,
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(!behaviour.IsInitialized || !behaviour.IsActive);
            if (GUILayout.Button("Lose Balance"))
            {
                behaviour.LoseBalance();
            }

            if (GUILayout.Button("Try Begin Get Up"))
            {
                behaviour.TryBeginGetUp();
            }

            if (GUILayout.Button("Begin Get Up Immediately"))
            {
                behaviour.BeginGetUpImmediately();
            }

            if (GUILayout.Button("Interrupt Get Up"))
            {
                behaviour.InterruptGetUp();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
