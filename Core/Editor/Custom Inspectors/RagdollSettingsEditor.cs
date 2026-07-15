using Hairibar.NaughtyExtensions.Editor;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Editor
{
    [CustomEditor(typeof(RagdollSettings))]
    internal class RagdollSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty bindingsDefinition;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bindingsDefinition.serializedObject.Update();

            ExtraNaughtyEditorGUILayout.Header("Ragdoll Profile");
            DrawProfileField(serializedObject.FindProperty("_powerProfile"));
            DrawProfileField(serializedObject.FindProperty("_weightDistribution"));

            ExtraNaughtyEditorGUILayout.Header("Limit Parameters");
            DrawLimitProperties();

            ExtraNaughtyEditorGUILayout.Header("Joint Processing");
            DrawJointProcessingProperties();

            ExtraNaughtyEditorGUILayout.Header("Rigidbody Settings");
            DrawRigidbodySettings();

            ExtraNaughtyEditorGUILayout.Header("Advanced Solver");
            DrawAdvancedSolverSettings();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawProfileField(SerializedProperty property)
        {
            UsePropertySetterDrawer.Draw_Layout(property);
            RagdollProfileEditorUtility.ValidateProfileField_Layout(property, bindingsDefinition.objectReferenceValue as RagdollDefinition, true);
        }

        void DrawLimitProperties()
        {
            EditorGUILayout.Slider(serializedObject.FindProperty("limitBounciness"), 0, 1);

            EditorGUILayout.Slider(serializedObject.FindProperty("limitContactDistanceFactor"), 0, 1,
                new GUIContent("Limit Contact Distance Factor", "How far the joint can \"see\" the limit, expressed as a factor of the total distance of the limit." +
                "1 means everywhere. 0.5 means halfwat through the joint. 0 lets PhysX pick the distance. \n" +
                "Increase this value to decrease jittering, at the cost of performance.")
                );

            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("limitSpring"),
                new GUIContent("Limit Spring",
                "The strength of the springs that enforce the joint limits. The value for each specific Joint is scaled by the mass of the attached Rigidbody."),
                0, Mathf.Infinity);

            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("limitSpringDamping"),
                new GUIContent("Limit Spring Damping",
                "The damping of the springs that enforce the joint limits. The value for each specific Joint is scaled by the mass of the attached Rigidbody."),
                0, Mathf.Infinity);
        }

        void DrawJointProcessingProperties()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableJointPreProcessing"),
                new GUIContent("Enable Joint Preprocessing",
                "Preprocessing is useful for fixing strange behaviour resulting from frozen rotation axes."));

            SerializedProperty enableJointProjection = serializedObject.FindProperty("enableJointProjection");
            EditorGUILayout.PropertyField(enableJointProjection,
                new GUIContent("Enable Joint Projection",
                "Projection \"cheats\" by bringing joints back in place even when the constraints are violated. " +
                "It is not a physical process, so it is best not to use it unless constraint violation is an issue."));

            if (enableJointProjection.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minJointProjectionDistance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minJointProjectionAngle"));
            }
        }

        void DrawRigidbodySettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useGravity"));
            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("totalMass"),
                new GUIContent("Total Mass", "The total mass of the Rigidbody. It will be distributed according to the assigned WeightDistribution."),
                0, Mathf.Infinity);
            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("drag"), 0, Mathf.Infinity);
            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("angularDrag"), 0, Mathf.Infinity);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("material"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interpolation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("collisionDetectionMode"));

            EditorGUILayout.IntSlider(serializedObject.FindProperty("solverIterations"), 6, 40,
                new GUIContent("Joint Solver Iterations",
                "The higher the iterations, the more accurate the Joint behaviour will be, at the cost of lower performance."));
        }


        void DrawAdvancedSolverSettings()
        {
            EditorGUILayout.IntSlider(
                serializedObject.FindProperty("solverVelocityIterations"),
                1,
                40,
                new GUIContent(
                    "Solver Velocity Iterations",
                    "Improves the accuracy of joint and collision exit velocities. Higher values cost more CPU time."));
            ClampedFloatDrawer.Draw_Layout(
                serializedObject.FindProperty("maxAngularVelocity"),
                new GUIContent(
                    "Maximum Angular Velocity",
                    "Radians per second. Unity clamps angular velocity before each simulation step to limit numerical instability."),
                0f,
                Mathf.Infinity);
            ClampedFloatDrawer.Draw_Layout(
                serializedObject.FindProperty("maxDepenetrationVelocity"),
                new GUIContent(
                    "Maximum Depenetration Velocity",
                    "Limits how quickly overlapping bodies are separated. Lower values are smoother but may remain overlapped longer."),
                0f,
                Mathf.Infinity);

            SerializedProperty overrideSleep =
                serializedObject.FindProperty("overrideSleepThreshold");
            EditorGUILayout.PropertyField(
                overrideSleep,
                new GUIContent(
                    "Override Sleep Threshold",
                    "Enable this to keep active ragdolls awake with a threshold of zero, or to author another per-body sleep threshold."));
            if (overrideSleep.boolValue)
            {
                ClampedFloatDrawer.Draw_Layout(
                    serializedObject.FindProperty("sleepThreshold"),
                    0f,
                    Mathf.Infinity);
            }

            SerializedProperty inertiaMode =
                serializedObject.FindProperty("inertiaTensorMode");
            EditorGUILayout.PropertyField(inertiaMode);
            if ((RagdollInertiaTensorMode) inertiaMode.enumValueIndex
                == RagdollInertiaTensorMode.ResetAndStabilize)
            {
                ClampedFloatDrawer.Draw_Layout(
                    serializedObject.FindProperty("maximumInertiaTensorRatio"),
                    new GUIContent(
                        "Maximum Inertia Tensor Ratio",
                        "Raises very small positive principal inertia values so the largest axis is no more than this multiple of the smallest movable axis."),
                    1f,
                    Mathf.Infinity);
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("angularDriveInertiaMode"),
                new GUIContent(
                    "Angular Drive Inertia",
                    "Rigidbody Mass preserves legacy tuning. Average Inertia gives the rotational drive physically meaningful torque scaling."));

            EditorGUILayout.HelpBox(
                "Compatibility defaults reproduce the previous solver behaviour. For an active ragdoll starting point, try 20/6 solver iterations, 20 rad/s maximum angular velocity, 5 depenetration velocity, zero sleep threshold, Reset And Stabilize with ratio 10, and Average Inertia.",
                MessageType.Info);
        }

        void OnEnable()
        {
            SerializedObject bindings = new SerializedObject((serializedObject.targetObject as RagdollSettings).GetComponent<RagdollDefinitionBindings>());
            bindingsDefinition = bindings.FindProperty("_definition");
        }
    }
}