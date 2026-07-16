using Hairibar.EngineExtensions.Editor;
using Hairibar.NaughtyExtensions.Editor;
using Hairibar.Ragdoll.Editor;
using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

#pragma warning disable 649
namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollAnimator))]
    internal class RagdollAnimatorEditor : UnityEditor.Editor
    {
        const string EDITOR_STATE_DIRECTORY = "Temp/Packages/com.hairibar.ragdoll/EditorState/RagdollAnimator/";

        [SerializeField] bool hideMesh;

        bool isInitialized;

        SerializedProperty bindingDefinition;
        Animator animator;

        #region Global Shortcut
        [ClutchShortcut("Hairibar.Ragdoll.RagdollAnimator/ForceTargetPose", KeyCode.P, ShortcutModifiers.Action, displayName = "Force Target Pose")]
        public static void ForceAnimatedPose(ShortcutArguments args)
        {
            if (!Application.isPlaying) return;

            if (args.stage == ShortcutStage.Begin)
            {
                SetForceAnimatedPoseGlobally(true);
            }
            else if (args.stage == ShortcutStage.End)
            {
                SetForceAnimatedPoseGlobally(false);
            }


            void SetForceAnimatedPoseGlobally(bool value)
            {
                foreach (RagdollAnimator ragdollAnimator in FindObjectsOfType<RagdollAnimator>())
                {
                    ragdollAnimator.forceTargetPose = value;
                }
            }
        }
        #endregion

        public override void OnInspectorGUI()
        {
            bindingDefinition?.serializedObject.Update();

            DrawBindingsField();

            ExtraNaughtyEditorGUILayout.Header("Animation Parameters");
            DrawProfileField();
            ClampedFloatDrawer.Draw_Layout(serializedObject.FindProperty("_profileTransitionLength"),
                new GUIContent("Profile Transition Length",
                "When changing profile, a blend transition will be done. This is the length of that transition."),
                0, Mathf.Infinity);

            ExtraNaughtyEditorGUILayout.Header("Master controls");
            NonLinearSliderDrawer.Draw_Layout(serializedObject.FindProperty("_masterAlpha"), 0, 1, QuadraticSliderDrawer.GetQuadraticFunction(2),
                new GUIContent("Master Alpha", "The profile's alpha values will be multiplied by this amount. \n" +
                "Alpha defines the stiffness with which the ragdoll matches the animation. " +
                "High values will instantly get to the target pose, while low values will treat the target pose more like a suggestion."));

            EditorGUILayout.Slider(serializedObject.FindProperty("_masterDampingRatio"), 0, 1,
                new GUIContent("Master Damping Ratio", "The profile's damping ratio values will be multiplied by this amount. \n" +
                "A damping ratio of 1 will get to the target pose perfectly, with no overshooting. " +
                "Lower values will overshoot the target pose."));

            ExtraNaughtyEditorGUILayout.Header("Advanced Pinning");
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("pinSettings"),
                new GUIContent(
                    "Pin Settings",
                    "Shapes temporary world-space position authority. Pin Pow curves weights between zero and one; Pin Distance Falloff loosens position pinning as a bone drifts from its Target; Angular Pinning adds an optional world-space torque channel without replacing the muscle Slerp Drive."),
                true);

            ExtraNaughtyEditorGUILayout.Header("Mapping");
            EditorGUILayout.Slider(serializedObject.FindProperty("_masterMappingWeight"), 0, 1,
                new GUIContent("Master Mapping Weight",
                "Multiplies position and rotation mapping for every bone. Zero keeps the target fully animated; one uses the configured per-bone weights."));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_defaultMappingWeights"),
                new GUIContent("Default Mapping Weights"),
                true);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_mappingOverrides"),
                new GUIContent("Bone Mapping Overrides"),
                true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("forceTargetPose"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("fixTargetTransforms"),
                new GUIContent(
                    "Fix Target Transforms",
                    "Restores each bound Target bone to its captured local pose before animation evaluation. This prevents additive read/write drift on bones that are not animated every frame."));

            ExtraNaughtyEditorGUILayout.Header("Lifecycle");
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("lifecycleState"),
                new GUIContent(
                    "State",
                    "Alive runs animation matching normally. Dead releases position pinning and blends rotational muscle strength. Frozen first completes Dead, waits until every ragdoll bone is below Max Freeze Sqr Velocity, then suspends the Puppet hierarchy."));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("lifecycleSettings"),
                new GUIContent("State Settings"),
                true);

            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
            {
                RagdollAnimator runtimeAnimator = (RagdollAnimator)target;
                EditorGUILayout.LabelField(
                    "Active State",
                    runtimeAnimator.ActiveState.ToString());
                EditorGUILayout.LabelField(
                    "Killing",
                    runtimeAnimator.IsKilling.ToString());
                EditorGUILayout.LabelField(
                    "Waiting For Freeze",
                    runtimeAnimator.IsWaitingForFreeze.ToString());
                EditorGUILayout.LabelField(
                    "Freeze Ready",
                    runtimeAnimator.FreezeReady.ToString());
                EditorGUILayout.LabelField(
                    "Maximum Puppet Sqr Velocity",
                    runtimeAnimator.MaximumPuppetSqrVelocity.ToString("0.####"));
                EditorGUILayout.LabelField(
                    "Kill Progress",
                    runtimeAnimator.KillProgress.ToString("P0"));
                EditorGUILayout.LabelField(
                    "Teleport Pending",
                    runtimeAnimator.HasPendingTeleport.ToString());
            }

            ExtraNaughtyEditorGUILayout.Header("Debug Features (Editor Only)");
            if (serializedObject.isEditingMultipleObjects)
            {
                NaughtyEditorGUI.HelpBox_Layout("Multiple object editing isn't supported for debug features.", MessageType.Info, logToConsole: false);
            }
            else
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    DoHideMeshField();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Field Drawers
        void DrawBindingsField()
        {
            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            EditorGUI.BeginChangeCheck();

            SerializedProperty bindings = serializedObject.FindProperty("_ragdollBindings");
            SerializedProperty targetBindings = serializedObject.FindProperty("_targetBindings");
            EditorGUILayout.PropertyField(bindings);
            EditorGUILayout.PropertyField(
                targetBindings,
                new GUIContent("Target Bindings"));

            bool bindingChanged = EditorGUI.EndChangeCheck();

            RagdollDefinitionBindings ragdollBindings =
                bindings.objectReferenceValue as RagdollDefinitionBindings;
            RagdollTargetBindings explicitTargetBindings =
                targetBindings.objectReferenceValue as RagdollTargetBindings;

            if (!ragdollBindings)
            {
                bindingDefinition = null;
                NaughtyEditorGUI.HelpBox_Layout(
                    "A RagdollDefinitionBindings must be assigned to the RagdollAnimator.",
                    MessageType.Error);
            }
            else
            {
                if (bindingChanged || bindingDefinition == null)
                {
                    bindingDefinition = new SerializedObject(ragdollBindings)
                        .FindProperty("_definition");
                }

                if (!explicitTargetBindings)
                {
                    EditorGUILayout.HelpBox(
                        "This component will use the legacy name-based Target lookup at runtime. "
                        + "Create explicit bindings so Target and ragdoll bone names and local axes can differ.",
                        MessageType.Warning);

                    EditorGUI.BeginDisabledGroup(serializedObject.isEditingMultipleObjects);
                    if (GUILayout.Button("Create Explicit Target Bindings"))
                    {
                        CreateExplicitTargetBindings(
                            ragdollBindings,
                            targetBindings);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                else if (explicitTargetBindings.RagdollBindings != ragdollBindings)
                {
                    EditorGUILayout.HelpBox(
                        "The assigned Target Bindings reference a different ragdoll.",
                        MessageType.Error);
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        void CreateExplicitTargetBindings(
            RagdollDefinitionBindings ragdollBindings,
            SerializedProperty targetBindingsProperty)
        {
            RagdollAnimator ragdollAnimator = (RagdollAnimator) target;
            RagdollTargetBindings explicitBindings =
                ragdollAnimator.GetComponent<RagdollTargetBindings>();

            if (!explicitBindings)
            {
                explicitBindings = Undo.AddComponent<RagdollTargetBindings>(
                    ragdollAnimator.gameObject);
            }

            Undo.RecordObject(explicitBindings, "Create explicit Target bindings");
            explicitBindings.SetRagdollBindings(ragdollBindings);

            string error;
            if (!explicitBindings.TryAutoBindByName(out error))
            {
                Debug.LogError(error, explicitBindings);
                return;
            }

            targetBindingsProperty.objectReferenceValue = explicitBindings;
            EditorUtility.SetDirty(explicitBindings);
        }

        void DrawProfileField()
        {
            SerializedProperty profile = serializedObject.FindProperty("currentProfile");
            UsePropertySetterDrawer.Draw_Layout(profile);
            RagdollProfileEditorUtility.ValidateProfileField_Layout(profile, bindingDefinition?.objectReferenceValue as RagdollDefinition, true);
        }


        void DoHideMeshField()
        {
            EditorGUI.BeginChangeCheck();
            hideMesh = EditorGUILayout.Toggle("Hide Mesh", hideMesh);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshHideMesh();
            }

            RefreshAnimatorCulling();
        }
        #endregion

        #region Value Appliers
        void RefreshHideMesh()
        {
            foreach (Renderer renderer in (target as RagdollAnimator).GetComponentsInChildren<Renderer>(true))
            {
                renderer.forceRenderingOff = hideMesh;
            }
        }

        void RefreshAnimatorCulling()
        {
            if (hideMesh && Application.isPlaying && animator) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        #endregion

        #region Lifetime
        void OnEnable()
        {
            if (isInitialized) return;

            animator = (target as RagdollAnimator).GetComponent<Animator>();

            EditorSerializationUtility.Deserialize(EDITOR_STATE_DIRECTORY, this, target);
            RefreshHideMesh();

            isInitialized = true;
        }

        void OnDisable()
        {
            if (!isInitialized) return;

            EditorSerializationUtility.Serialize(EDITOR_STATE_DIRECTORY, this, target);
            isInitialized = false;
        }
        #endregion
    }
}
