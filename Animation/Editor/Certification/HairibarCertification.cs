using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hairibar.Ragdoll.Animation.Editor
{
    /// <summary>
    /// Project-external certification orchestrator. Generated assets and build outputs
    /// live in the validation project or the configured temporary output directory.
    /// </summary>
    public static class HairibarCertification
    {
        const string PackageName = "com.hairibar.ragdoll";
        const string GeneratedRoot = "Assets/__HairibarCertification";
        const string ControllerPath =
            GeneratedRoot + "/HairibarCertification.controller";
        const string LocomotionClipPath =
            GeneratedRoot + "/HairibarCertificationLocomotion.anim";
        const string RegressionRigPath =
            GeneratedRoot + "/HairibarRegressionRig.prefab";
        const string RegressionDefinitionPath =
            GeneratedRoot + "/HairibarRegressionDefinition.asset";
        const string RegressionProfilePath =
            GeneratedRoot + "/HairibarRegressionProfile.asset";

        [Serializable]
        sealed class BuildEntry
        {
            public string target;
            public bool succeeded;
            public string output;
            public string error;
            public ulong totalSize;
        }

        [Serializable]
        sealed class BuildManifest
        {
            public string unityVersion;
            public string generatedUtc;
            public BuildEntry[] builds;
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Prepare Assets")]
        public static void PrepareAssets()
        {
            Sample sample = FindDemoSample();
            if (!sample.isImported)
            {
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            EnsureGeneratedFolder();
            string samplePath = NormalizeAssetPath(sample.importPath);
            GameObject humanoid = FindHumanoid(samplePath);
            ValidateHumanoid(humanoid);
            CreateAnimatorController(samplePath);
            GameObject regressionRig = CreateRegressionRigPrefab();
            RagdollAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<RagdollAnimationProfile>(
                    RegressionProfilePath);
            BindRegressionScenes(
                samplePath,
                humanoid,
                regressionRig,
                profile,
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    ControllerPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Run All Builds")]
        public static void RunAll()
        {
            PrepareAssets();
            Sample importedSample = FindDemoSample();
            string outputRoot = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CERTIFICATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(
                    Path.GetTempPath(),
                    "HairibarRagdollCertification");
            }
            Directory.CreateDirectory(outputRoot);

            string[] scenes = FindRegressionScenes(
                NormalizeAssetPath(importedSample.importPath));
            List<BuildEntry> entries = new List<BuildEntry>(4)
            {
                Build(
                    BuildTarget.StandaloneWindows64,
                    scenes,
                    Path.Combine(outputRoot, "Windows64", "HairibarCertification.exe"),
                    true,
                    outputRoot),
                Build(
                    BuildTarget.StandaloneLinux64,
                    scenes,
                    Path.Combine(outputRoot, "Linux64", "HairibarCertification.x86_64"),
                    false,
                    outputRoot),
                Build(
                    BuildTarget.StandaloneOSX,
                    scenes,
                    Path.Combine(outputRoot, "macOS", "HairibarCertification.app"),
                    false,
                    outputRoot),
                Build(
                    BuildTarget.WebGL,
                    scenes,
                    Path.Combine(outputRoot, "WebGL"),
                    false,
                    outputRoot)
            };

            BuildManifest manifest = new BuildManifest
            {
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                builds = entries.ToArray()
            };
            string manifestPath = Path.Combine(outputRoot, "build-manifest.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

            for (int index = 0; index < entries.Count; index++)
            {
                if (!entries[index].succeeded)
                {
                    throw new InvalidOperationException(
                        "Certification failed for " + entries[index].target
                        + ": " + entries[index].error);
                }
            }
            UnityEngine.Debug.Log(
                "Hairibar certification builds succeeded. Manifest: "
                + manifestPath);
        }

        static Sample FindDemoSample()
        {
            IEnumerable<Sample> samples = Sample.FindByPackage(
                PackageName,
                PackageInfoVersion());
            foreach (Sample sample in samples)
            {
                if (sample.displayName == "Demo Scenes")
                {
                    return sample;
                }
            }
            throw new InvalidOperationException(
                "The package does not expose the Demo Scenes sample.");
        }

        static string PackageInfoVersion()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(RagdollAnimator).Assembly);
            if (package == null || string.IsNullOrEmpty(package.version))
            {
                throw new InvalidOperationException(
                    "Could not resolve the installed Hairibar package version.");
            }
            return package.version;
        }

        static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__HairibarCertification");
            }
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Run WebGL Build")]
        public static void RunWebGL()
        {
            PrepareAssets();
            Sample importedSample = FindDemoSample();
            string outputRoot = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CERTIFICATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(
                    Path.GetTempPath(),
                    "HairibarRagdollCertification-WebGL");
            }
            Directory.CreateDirectory(outputRoot);
            BuildEntry entry = Build(
                BuildTarget.WebGL,
                FindRegressionScenes(NormalizeAssetPath(importedSample.importPath)),
                Path.Combine(outputRoot, "WebGL"),
                false,
                outputRoot);
            BuildManifest manifest = new BuildManifest
            {
                unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                builds = new[] { entry }
            };
            File.WriteAllText(
                Path.Combine(outputRoot, "webgl-build-manifest.json"),
                JsonUtility.ToJson(manifest, true));
            if (!entry.succeeded)
            {
                throw new InvalidOperationException(
                    "Certification failed for WebGL: " + entry.error);
            }
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Run Windows Player")]
        public static void RunWindows()
        {
            PrepareAssets();
            Sample importedSample = FindDemoSample();
            string outputRoot = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CERTIFICATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = Path.Combine(Path.GetTempPath(),
                    "HairibarRagdollCertification-Windows");
            Directory.CreateDirectory(outputRoot);
            BuildEntry entry = Build(
                BuildTarget.StandaloneWindows64,
                FindRegressionScenes(NormalizeAssetPath(importedSample.importPath)),
                Path.Combine(outputRoot, "Windows64", "HairibarCertification.exe"),
                true,
                outputRoot);
            File.WriteAllText(
                Path.Combine(outputRoot, "windows-build-manifest.json"),
                JsonUtility.ToJson(new BuildManifest
                {
                    unityVersion = Application.unityVersion,
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    builds = new[] { entry }
                }, true));
            if (!entry.succeeded)
                throw new InvalidOperationException(
                    "Certification failed for Windows64: " + entry.error);
        }

        static string NormalizeAssetPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return normalized;
            }
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."))
                .Replace('\\', '/')
                .TrimEnd('/');
            string fullPath = Path.GetFullPath(path)
                .Replace('\\', '/');
            if (!fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Sample import path is outside the active Unity project: " + path);
            }
            return fullPath.Substring(projectRoot.Length + 1);
        }

        static GameObject FindHumanoid(string samplePath)
        {
            string[] guids = AssetDatabase.FindAssets(
                "FBX_MixamoBot t:GameObject",
                new[] { samplePath });
            if (guids.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one imported FBX_MixamoBot asset, found "
                    + guids.Length + ".");
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static void ValidateHumanoid(GameObject humanoid)
        {
            Animator animator = humanoid
                ? humanoid.GetComponentInChildren<Animator>(true)
                : null;
            if (!animator
                || !animator.avatar
                || !animator.avatar.isValid
                || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    "MixamoBot must import with a valid Humanoid Avatar.");
            }
        }

        static GameObject CreateRegressionRigPrefab()
        {
            AssetDatabase.DeleteAsset(RegressionRigPath);
            AssetDatabase.DeleteAsset(RegressionDefinitionPath);
            AssetDatabase.DeleteAsset(RegressionProfilePath);

            RagdollDefinition definition =
                ScriptableObject.CreateInstance<RagdollDefinition>();
            SerializedObject definitionObject = new SerializedObject(definition);
            definitionObject.FindProperty("_isValid").boolValue = true;
            definitionObject.FindProperty("_root")
                .FindPropertyRelative("name").stringValue = "Root";
            SerializedProperty bones = definitionObject.FindProperty("bones");
            bones.arraySize = 2;
            bones.GetArrayElementAtIndex(0).FindPropertyRelative("name")
                .stringValue = "Root";
            bones.GetArrayElementAtIndex(1).FindPropertyRelative("name")
                .stringValue = "Child";
            definitionObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, RegressionDefinitionPath);

            RagdollAnimationProfile profile =
                ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            AssetDatabase.CreateAsset(profile, RegressionProfilePath);

            GameObject container = new GameObject("Hairibar Regression Rig");
            try
            {
                GameObject target = new GameObject("Root");
                target.transform.SetParent(container.transform, false);
                GameObject targetChild = new GameObject("Child");
                targetChild.transform.SetParent(target.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                target.AddComponent<UnityEngine.Animation>().animatePhysics = true;

                GameObject puppet = new GameObject("Root");
                puppet.SetActive(false);
                puppet.transform.SetParent(container.transform, false);
                GameObject puppetChild = new GameObject("Child");
                puppetChild.transform.SetParent(puppet.transform, false);
                puppetChild.transform.localPosition = Vector3.up;
                Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
                rootBody.mass = 10f;
                ConfigurableJoint rootJoint =
                    puppet.AddComponent<ConfigurableJoint>();
                puppet.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;
                Rigidbody childBody = puppetChild.AddComponent<Rigidbody>();
                childBody.mass = 2f;
                ConfigurableJoint childJoint =
                    puppetChild.AddComponent<ConfigurableJoint>();
                childJoint.connectedBody = rootBody;
                puppetChild.AddComponent<BoxCollider>().size = Vector3.one * 0.35f;

                RagdollDefinitionBindings bindings =
                    puppet.AddComponent<RagdollDefinitionBindings>();
                SerializedObject bindingsObject = new SerializedObject(bindings);
                bindingsObject.FindProperty("_definition").objectReferenceValue =
                    definition;
                SerializedProperty dictionary =
                    bindingsObject.FindProperty("bindings");
                SerializedProperty keys = dictionary.FindPropertyRelative("keys");
                SerializedProperty values =
                    dictionary.FindPropertyRelative("values");
                keys.arraySize = 2;
                values.arraySize = 2;
                keys.GetArrayElementAtIndex(0).FindPropertyRelative("name")
                    .stringValue = "Root";
                keys.GetArrayElementAtIndex(1).FindPropertyRelative("name")
                    .stringValue = "Child";
                values.GetArrayElementAtIndex(0).objectReferenceValue = rootJoint;
                values.GetArrayElementAtIndex(1).objectReferenceValue = childJoint;
                bindingsObject.ApplyModifiedPropertiesWithoutUndo();
                puppet.SetActive(true);

                return PrefabUtility.SaveAsPrefabAsset(container, RegressionRigPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        static void BindRegressionScenes(
            string samplePath,
            GameObject humanoid,
            GameObject regressionRig,
            RagdollAnimationProfile profile,
            RuntimeAnimatorController controller)
        {
            string[] names =
            {
                "CoreLifecycle", "HumanoidBakerFall",
                "HierarchyProps", "CollisionsPerformance"
            };
            for (int sceneIndex = 0; sceneIndex < names.Length; sceneIndex++)
            {
                string[] guids = AssetDatabase.FindAssets(
                    names[sceneIndex] + " t:Scene",
                    new[] { samplePath + "/Regression" });
                if (guids.Length != 1)
                {
                    throw new InvalidOperationException(
                        names[sceneIndex]
                        + " regression scene was not imported exactly once.");
                }
                Scene scene = EditorSceneManager.OpenScene(
                    AssetDatabase.GUIDToAssetPath(guids[0]),
                    OpenSceneMode.Additive);
                bool assigned = false;
                try
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        MonoBehaviour[] behaviours = roots[rootIndex]
                            .GetComponentsInChildren<MonoBehaviour>(true);
                        for (int index = 0; index < behaviours.Length; index++)
                        {
                            MonoBehaviour behaviour = behaviours[index];
                            if (!behaviour || behaviour.GetType().FullName
                                != "Hairibar.Ragdoll.Demo.RegressionScenarioRunner")
                            {
                                continue;
                            }
                            SerializedObject serialized =
                                new SerializedObject(behaviour);
                            serialized.FindProperty("humanoidPrefab")
                                .objectReferenceValue = humanoid;
                            serialized.FindProperty("ragdollPrefab")
                                .objectReferenceValue = regressionRig;
                            serialized.FindProperty("animationProfile")
                                .objectReferenceValue = profile;
                            serialized.FindProperty("humanoidController")
                                .objectReferenceValue = controller;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            assigned = true;
                        }
                    }
                    if (!assigned)
                        throw new InvalidOperationException(
                            names[sceneIndex] + " has no RegressionScenarioRunner.");
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static void CreateAnimatorController(string samplePath)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AssetDatabase.DeleteAsset(LocomotionClipPath);
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("FallBlend", AnimatorControllerParameterType.Float);
            controller.AddParameter("GetUp", AnimatorControllerParameterType.Trigger);

            AnimationClip idle = FindAnimationClip(samplePath, "Idle");
            ConfigureHorizontalRootMotionImport(samplePath, "Jog");
            AnimationClip jogSource = FindAnimationClip(samplePath, "Jog");
            AnimationClip locomotion = UnityEngine.Object.Instantiate(jogSource);
            locomotion.name = "Hairibar Certification Locomotion";
            AnimationUtility.SetEditorCurve(
                locomotion,
                EditorCurveBinding.FloatCurve(
                    string.Empty, typeof(Animator), "MotionT.x"),
                AnimationCurve.Linear(
                    0f, 0f, Mathf.Max(0.1f, locomotion.length), 1f));
            AnimationUtility.SetAnimationEvents(locomotion, new[]
            {
                new AnimationEvent
                {
                    functionName = "OnHairibarCertificationAnimationEvent",
                    time = Mathf.Min(0.1f, locomotion.length * 0.25f)
                }
            });
            if (!locomotion.humanMotion
                || !locomotion.hasRootCurves
                || !locomotion.hasMotionCurves)
            {
                throw new InvalidOperationException(
                    "The generated certification clip must remain Humanoid and contain root-motion curves.");
            }
            AssetDatabase.CreateAsset(locomotion, LocomotionClipPath);
            AnimationClip waving = FindAnimationClip(samplePath, "Waving");
            AnimatorControllerLayer baseLayer = controller.layers[0];
            baseLayer.iKPass = true;
            AnimatorState idleState = baseLayer.stateMachine.AddState("Locomotion");
            idleState.motion = locomotion;
            baseLayer.stateMachine.defaultState = idleState;
            AnimatorState fall = baseLayer.stateMachine.AddState("Fall");
            fall.motion = idle;
            AnimatorState prone = baseLayer.stateMachine.AddState("GetUp Prone");
            prone.motion = idle;
            AnimatorState supine = baseLayer.stateMachine.AddState("GetUp Supine");
            supine.motion = idle;
            AnimatorControllerLayer[] configuredLayers = controller.layers;
            configuredLayers[0] = baseLayer;
            controller.layers = configuredLayers;

            AnimatorControllerLayer upper = new AnimatorControllerLayer
            {
                name = "Upper Body",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                iKPass = true,
                stateMachine = new AnimatorStateMachine
                {
                    name = "Upper Body"
                }
            };
            AssetDatabase.AddObjectToAsset(upper.stateMachine, controller);
            AnimatorState wavingState = upper.stateMachine.AddState("Waving");
            wavingState.motion = waving;
            upper.stateMachine.defaultState = wavingState;
            controller.AddLayer(upper);
            EditorUtility.SetDirty(controller);
        }

        static void ConfigureHorizontalRootMotionImport(
            string samplePath,
            string token)
        {
            string[] guids = AssetDatabase.FindAssets(token, new[] { samplePath });
            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                ModelImporter importer = AssetImporter.GetAtPath(assetPath)
                    as ModelImporter;
                if (!importer) continue;
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    clips[clipIndex].lockRootPositionXZ = false;
                    clips[clipIndex].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                return;
            }
            throw new InvalidOperationException(
                "No ModelImporter contains animation token '" + token + "'.");
        }

        static AnimationClip FindAnimationClip(string samplePath, string token)
        {
            string[] guids = AssetDatabase.FindAssets(token, new[] { samplePath });
            for (int index = 0; index < guids.Length; index++)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    AnimationClip clip = assets[assetIndex] as AnimationClip;
                    if (clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    {
                        return clip;
                    }
                }
            }
            throw new InvalidOperationException(
                "No imported AnimationClip contains token '" + token + "'.");
        }

        static string[] FindRegressionScenes(string sampleRoot)
        {
            sampleRoot += "/Regression";
            string[] names =
            {
                "CoreLifecycle",
                "HumanoidBakerFall",
                "HierarchyProps",
                "CollisionsPerformance"
            };
            string[] result = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string[] guids = AssetDatabase.FindAssets(
                    names[index] + " t:Scene",
                    new[] { sampleRoot });
                if (guids.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Regression scene '" + names[index]
                        + "' was not imported exactly once.");
                }
                result[index] = AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return result;
        }

        static BuildEntry Build(
            BuildTarget target,
            string[] scenes,
            string output,
            bool execute,
            string outputRoot)
        {
            BuildEntry entry = new BuildEntry
            {
                target = target.ToString(),
                output = output
            };
            BuildTargetGroup group = target == BuildTarget.WebGL
                ? BuildTargetGroup.WebGL
                : BuildTargetGroup.Standalone;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                entry.error = "The required playback engine is not installed.";
                return entry;
            }

            Il2CppCompilerConfiguration previousCompiler = default;
            bool changedCompiler = false;
            try
            {
                if (target == BuildTarget.WebGL)
                {
                    previousCompiler =
                        PlayerSettings.GetIl2CppCompilerConfiguration(
                            NamedBuildTarget.WebGL);
                    PlayerSettings.SetIl2CppCompilerConfiguration(
                        NamedBuildTarget.WebGL,
                        Il2CppCompilerConfiguration.Debug);
                    changedCompiler = true;
                }
                string directory = target == BuildTarget.WebGL
                    ? output
                    : Path.GetDirectoryName(output);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                Directory.CreateDirectory(directory);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging
                });
                entry.totalSize = report.summary.totalSize;
                if (report.summary.result != BuildResult.Succeeded)
                {
                    entry.error = report.summary.result.ToString();
                    return entry;
                }

                if (execute)
                {
                    ExecuteWindowsPlayer(output, outputRoot);
                }
                entry.succeeded = true;
                return entry;
            }
            catch (Exception exception)
            {
                entry.error = exception.ToString();
                return entry;
            }
            finally
            {
                if (changedCompiler)
                {
                    PlayerSettings.SetIl2CppCompilerConfiguration(
                        NamedBuildTarget.WebGL,
                        previousCompiler);
                }
            }
        }

        static void ExecuteWindowsPlayer(string executable, string outputRoot)
        {
            string playerDirectory = Path.GetDirectoryName(executable);
            string resultPath = Path.Combine(
                outputRoot,
                "windows-player-result.json");
            if (File.Exists(resultPath)) File.Delete(resultPath);
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "-batchmode -nographics -logFile \""
                    + Path.Combine(playerDirectory, "player.log") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = playerDirectory
            };
            start.EnvironmentVariables["HAIRIBAR_CERTIFICATION_RESULT"] =
                resultPath;
            using (Process process = Process.Start(start))
            {
                if (process == null)
                {
                    throw new InvalidOperationException(
                        "Windows certification Player did not start.");
                }
                if (!process.WaitForExit(300000))
                {
                    process.Kill();
                    throw new TimeoutException(
                        "Windows certification Player exceeded five minutes.");
                }
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Windows certification Player exited with code "
                        + process.ExitCode + ".");
                }
            }
            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "Windows certification Player produced no JSON result.");
            }
            PlayerResult result = JsonUtility.FromJson<PlayerResult>(
                File.ReadAllText(resultPath));
            if (result == null || !result.succeeded)
            {
                throw new InvalidOperationException(
                    "Windows certification JSON reports a failed scenario.");
            }
        }

        [Serializable]
        sealed class PlayerResult
        {
            public bool succeeded;
        }
    }
}
