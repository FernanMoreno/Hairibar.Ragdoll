using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollPhysicsQualityProfile))]
    internal sealed class RagdollPhysicsQualityProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollPhysicsQualityProfile profile =
                (RagdollPhysicsQualityProfile) target;
            EditorGUILayout.Space();

            string error;
            bool isValid = profile.TryValidate(out error);
            EditorGUILayout.HelpBox(
                isValid
                    ? "The quality bands are valid. Distances are evaluated from near to far and simulation modes may only become cheaper."
                    : error,
                isValid ? MessageType.Info : MessageType.Error);

            if (GUILayout.Button("Reset To Recommended Levels"))
            {
                Undo.RecordObject(profile, "Reset Ragdoll Physics Quality Levels");
                profile.ResetToRecommendedLevels();
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
