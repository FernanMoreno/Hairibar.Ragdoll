using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollPhysicsQualityBudget))]
    internal sealed class RagdollPhysicsQualityBudgetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying) return;
            RagdollPhysicsQualityBudget budget =
                (RagdollPhysicsQualityBudget) target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Registered controllers: " + budget.RegisteredCount
                + "\nActive grants: " + budget.ActiveGrantCount
                + " / " + budget.MaximumActiveRagdolls,
                MessageType.Info);

            if (GUILayout.Button("Evaluate Now")) budget.EvaluateNow();
        }
    }
}
