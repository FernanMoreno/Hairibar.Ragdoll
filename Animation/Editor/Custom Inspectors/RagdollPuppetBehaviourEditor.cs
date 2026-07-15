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
                    "Configure Ground Layers so they include walkable geometry. "
                    + "Body Front Axis must point out of the chest and Body Up Axis must match character up while standing.",
                    MessageType.Info);
                return;
            }

            RagdollGroundingSnapshot grounding = behaviour.Grounding;
            EditorGUILayout.HelpBox(
                "State: " + behaviour.State
                + "\nState time: " + behaviour.StateElapsedTime.ToString("F2")
                + "\nGetUp progress: " + behaviour.GetUpProgress.ToString("P0")
                + "\nOrientation: " + behaviour.GetUpOrientation
                + "\nGrounded: " + grounding.IsGrounded
                + "\nStable ground time: " + grounding.StableTime.ToString("F2")
                + "\nCOM speed: " + grounding.CenterOfMassVelocity.magnitude.ToString("F2")
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

            if (GUILayout.Button("Begin Prone Get Up Immediately"))
            {
                behaviour.BeginGetUpImmediately(RagdollGetUpOrientation.Prone);
            }

            if (GUILayout.Button("Begin Supine Get Up Immediately"))
            {
                behaviour.BeginGetUpImmediately(RagdollGetUpOrientation.Supine);
            }

            if (GUILayout.Button("Interrupt Get Up"))
            {
                behaviour.InterruptGetUp();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
