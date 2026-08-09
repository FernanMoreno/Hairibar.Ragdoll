using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
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
        const string GeneratedRegressionRoot =
            GeneratedRoot + "/Regression";
        const string RuntimeFixtureRoot =
            GeneratedRoot + "/Resources/HairibarCertification";
        const string RuntimeHumanoidPath =
            RuntimeFixtureRoot + "/Humanoid.prefab";
        const string RuntimeRigPath =
            RuntimeFixtureRoot + "/RegressionRig.prefab";
        const string RuntimeProfilePath =
            RuntimeFixtureRoot + "/RegressionProfile.asset";
        const string RuntimeHumanoidBindingsPath =
            RuntimeFixtureRoot + "/HumanoidBindings.asset";
        const string PendingOperationKey =
            "Hairibar.Ragdoll.Certification.PendingOperation";
        const string ContinuationScheduledKey =
            "Hairibar.Ragdoll.Certification.ContinuationScheduled";
        const string RunnerWaitStartedKey =
            "Hairibar.Ragdoll.Certification.RunnerWaitStarted";
        const string RegressionRunnerTypeName =
            "Hairibar.Ragdoll.Demo.RegressionScenarioRunner";

        enum PendingOperation
        {
            None,
            PrepareAssets,
            RunAll,
            RunWebGL,
            RunWindows
        }

        [Serializable]
        sealed class BuildDiagnosticEntry
        {
            public string severity;
            public bool own;
            public string message;
        }

        [Serializable]
        sealed class BuildEntry
        {
            public string target;
            public string result;
            public bool succeeded;
            public bool development;
            public bool allowDebugging;
            public bool outputExists;
            public string output;
            public string error;
            public ulong totalSize;
            public string[] ownDiagnostics;
            public string[] externalWarnings;
            public BuildDiagnosticEntry[] diagnostics;
        }

        [Serializable]
        sealed class BuildManifest
        {
            public int schemaVersion = 2;
            public string unityVersion;
            public string generatedUtc;
            public BuildEntry[] builds;
        }

        [Serializable]
        sealed class DocumentationAuditEntry
        {
            public string id;
            public string sourceUrl;
            public string affectedApi;
            public string exactTest;
            public string[] artifactKinds;
            public bool audited;
            public DocumentationMemberMapping[] members;
        }

        [Serializable]
        sealed class DocumentationMemberMapping
        {
            public string symbol;
            public string declaringType;
            public string memberName;
            public string memberKind;
            public string documentationSection;
            public string sourcePath;
            public bool verified;
        }

        [Serializable]
        sealed class DocumentationAuditManifest
        {
            public int schemaVersion = 2;
            public bool succeeded;
            public string documentPath;
            public string documentSha256;
            public DocumentationAuditEntry[] entries;
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Prepare Assets")]
        public static void PrepareAssets()
        {
            BeginWhenDemoScriptsAreAvailable(PendingOperation.PrepareAssets);
        }

        [InitializeOnLoadMethod]
        static void ResumePendingOperationAfterDomainReload()
        {
            if (GetPendingOperation() != PendingOperation.None)
            {
                SchedulePendingOperation();
            }
        }

        static void BeginWhenDemoScriptsAreAvailable(PendingOperation operation)
        {
            Sample sample = FindDemoSample();
            if (!sample.isImported || !SampleImportIsCurrent(sample))
            {
                SetPendingOperation(operation);
                SessionState.SetFloat(RunnerWaitStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                // Refresh imports assets synchronously, but Unity compiles newly
                // imported scripts after this editor callback returns. Persisting the
                // operation allows the InitializeOnLoad continuation to resume after
                // that domain reload instead of opening scenes with Missing Scripts.
                CompilationPipeline.RequestScriptCompilation();
                return;
            }

            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || FindRegressionScenarioRunnerType() == null)
            {
                SetPendingOperation(operation);
                if (!SessionState.GetBool(ContinuationScheduledKey, false))
                {
                    SessionState.SetFloat(RunnerWaitStartedKey,
                        (float)EditorApplication.timeSinceStartup);
                }
                SchedulePendingOperation();
                return;
            }

            ClearPendingOperation();
            ExecuteOperation(operation);
        }

        static void SchedulePendingOperation()
        {
            if (SessionState.GetBool(ContinuationScheduledKey, false)) return;
            SessionState.SetBool(ContinuationScheduledKey, true);
            EditorApplication.delayCall += ResumePendingOperation;
        }

        static void ResumePendingOperation()
        {
            SessionState.SetBool(ContinuationScheduledKey, false);
            PendingOperation operation = GetPendingOperation();
            if (operation == PendingOperation.None) return;
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    SchedulePendingOperation();
                    return;
                }
                if (FindRegressionScenarioRunnerType() == null)
                {
                    float waitStarted = SessionState.GetFloat(RunnerWaitStartedKey,
                        (float)EditorApplication.timeSinceStartup);
                    if (EditorApplication.timeSinceStartup - waitStarted < 60d)
                    {
                        SchedulePendingOperation();
                        return;
                    }
                    ThrowRunnerUnavailable();
                }

                ClearPendingOperation();
                ExecuteOperation(operation);
            }
            catch
            {
                // A failed continuation must not survive into the next domain
                // reload. Otherwise every reload retries a known-failed operation
                // and makes the certification command impossible to recover.
                ClearPendingOperation();
                throw;
            }
        }

        static void ThrowRunnerUnavailable()
        {
            // The timeout is terminal for this invocation. Erase SessionState before
            // throwing so the next domain reload does not re-arm a failed command.
            ClearPendingOperation();
            throw new InvalidOperationException(
                "The Demo Scenes sample is imported but "
                + RegressionRunnerTypeName
                + " is unavailable after script compilation. Check the "
                + "RegressionScenarioRunner compile errors before certification.");
        }

        static void ExecuteOperation(PendingOperation operation)
        {
            bool succeeded = false;
            try
            {
                switch (operation)
                {
                    case PendingOperation.PrepareAssets:
                        PrepareAssetsCore();
                        break;
                    case PendingOperation.RunAll:
                        PrepareAssetsCore();
                        RunAllCore();
                        break;
                    case PendingOperation.RunWebGL:
                        PrepareAssetsCore();
                        RunWebGLCore();
                        break;
                    case PendingOperation.RunWindows:
                        PrepareAssetsCore();
                        RunWindowsCore();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation));
                }
                succeeded = true;
            }
            finally
            {
                // In batch mode the first call can import source files and trigger a
                // domain reload. Do not pass -quit to that invocation: this explicit
                // exit runs only after the persisted continuation has completed.
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(succeeded ? 0 : 1);
                }
            }
        }

        internal static void PrepareAssetsCore()
        {
            Sample sample = FindDemoSample();
            if (!sample.isImported
                || !SampleImportIsCurrent(sample)
                || FindRegressionScenarioRunnerType() == null)
            {
                throw new InvalidOperationException(
                    "PrepareAssetsCore requires imported Demo Scenes and a compiled "
                    + RegressionRunnerTypeName + ".");
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
            CreateRuntimeHumanoidFixtures(humanoid);
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

        internal static bool SampleImportIsCurrent(Sample sample)
        {
            if (!sample.isImported
                || string.IsNullOrWhiteSpace(sample.resolvedPath)
                || string.IsNullOrWhiteSpace(sample.importPath))
            {
                return false;
            }

            string sourceRoot = Path.GetFullPath(sample.resolvedPath);
            string importedAssetPath = NormalizeAssetPath(sample.importPath);
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string importedRoot = Path.GetFullPath(
                Path.Combine(projectRoot, importedAssetPath));
            if (!Directory.Exists(sourceRoot) || !Directory.Exists(importedRoot))
            {
                return false;
            }

            return SamplePayloadTreesMatch(sourceRoot, importedRoot);
        }

        internal static bool SamplePayloadTreesMatch(
            string sourceRoot,
            string importedRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot)
                || string.IsNullOrWhiteSpace(importedRoot)
                || !Directory.Exists(sourceRoot)
                || !Directory.Exists(importedRoot))
            {
                return false;
            }
            sourceRoot = Path.GetFullPath(sourceRoot);
            importedRoot = Path.GetFullPath(importedRoot);
            string[] sourceFiles = GetSamplePayloadFiles(sourceRoot);
            string[] importedFiles = GetSamplePayloadFiles(importedRoot);
            if (sourceFiles.Length != importedFiles.Length) return false;

            for (int index = 0; index < sourceFiles.Length; index++)
            {
                string relative = Path.GetRelativePath(
                    sourceRoot,
                    sourceFiles[index]);
                string imported = Path.Combine(importedRoot, relative);
                if (!File.Exists(imported)
                    || !FilesHaveEqualContent(sourceFiles[index], imported))
                {
                    return false;
                }
            }
            return true;
        }

        static string[] GetSamplePayloadFiles(string root)
        {
            string[] files = Directory.GetFiles(
                root,
                "*",
                SearchOption.AllDirectories);
            List<string> payload = new List<string>(files.Length);
            for (int index = 0; index < files.Length; index++)
            {
                if (!files[index].EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
                {
                    payload.Add(files[index]);
                }
            }
            payload.Sort(StringComparer.OrdinalIgnoreCase);
            return payload.ToArray();
        }

        static bool FilesHaveEqualContent(string left, string right)
        {
            FileInfo leftInfo = new FileInfo(left);
            FileInfo rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length) return false;

            const int BufferSize = 81920;
            byte[] leftBuffer = new byte[BufferSize];
            byte[] rightBuffer = new byte[BufferSize];
            using (FileStream leftStream = File.OpenRead(left))
            using (FileStream rightStream = File.OpenRead(right))
            {
                while (true)
                {
                    int leftRead = leftStream.Read(
                        leftBuffer, 0, leftBuffer.Length);
                    int rightRead = rightStream.Read(
                        rightBuffer, 0, rightBuffer.Length);
                    if (leftRead != rightRead) return false;
                    if (leftRead == 0) return true;
                    for (int index = 0; index < leftRead; index++)
                    {
                        if (leftBuffer[index] != rightBuffer[index]) return false;
                    }
                }
            }
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Run All Builds")]
        public static void RunAll()
        {
            BeginWhenDemoScriptsAreAvailable(PendingOperation.RunAll);
        }

        /// <summary>
        /// CLI stage entry used by Tools~/Run-HairibarClosure.ps1. Unity tests are
        /// intentionally executed by separate official -runTests processes; this
        /// method dispatches only the non-test stage selected by
        /// HAIRIBAR_CLOSURE_PHASE.
        /// </summary>
        public static void RunClosure()
        {
            string phase = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CLOSURE_PHASE");
            switch (phase)
            {
                case "Prepare": PrepareAssets(); break;
                case "Build": RunAll(); break;
                case "Provisional": GenerateClosureProvisional(); break;
                case "Validate": ValidateClosureProvisional(); break;
                case "Finalize": FinalizeClosure(); break;
                default:
                    throw new InvalidOperationException(
                        "HAIRIBAR_CLOSURE_PHASE must be Prepare, Build, " +
                        "Provisional, Validate or Finalize. Invoke the durable " +
                        "Tools~/Run-HairibarClosure.ps1 coordinator.");
            }
        }

        public static void GenerateClosureProvisional()
        {
            string path = RagdollClosureCoordinator.GenerateProvisional();
            UnityEngine.Debug.Log("Hairibar provisional closure manifest: " + path);
        }

        public static void ValidateClosureProvisional()
        {
            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosureCoordinator.ValidateProvisional();
            if (!validation.succeeded)
                throw new InvalidDataException(
                    "Independent closure validation failed: "
                    + string.Join(" | ", validation.errors ?? Array.Empty<string>()));
        }

        public static void FinalizeClosure()
        {
            RagdollClosureCoordinator.FinalizeClosure();
        }

        static void RunAllCore()
        {
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
            WriteDocumentationAudit(outputRoot);
            UnityEngine.Debug.Log(
                "Hairibar certification builds succeeded. Manifest: "
                + manifestPath);
        }

        internal static Sample FindDemoSample()
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

        static void SetPendingOperation(PendingOperation operation)
        {
            SessionState.SetInt(PendingOperationKey, (int)operation);
        }

        static PendingOperation GetPendingOperation()
        {
            int value = SessionState.GetInt(PendingOperationKey, 0);
            return Enum.IsDefined(typeof(PendingOperation), value)
                ? (PendingOperation)value
                : PendingOperation.None;
        }

        static void ClearPendingOperation()
        {
            SessionState.EraseInt(PendingOperationKey);
            SessionState.EraseBool(ContinuationScheduledKey);
            SessionState.EraseFloat(RunnerWaitStartedKey);
        }

        static Type FindRegressionScenarioRunnerType()
        {
            foreach (System.Reflection.Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(RegressionRunnerTypeName, false);
                if (type != null) return type;
            }
            return null;
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

        static void EnsureAssetFolder(string path)
        {
            path = NormalizeAssetPath(path).TrimEnd('/');
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        [MenuItem("Tools/Hairibar Ragdoll/Certification/Run WebGL Build")]
        public static void RunWebGL()
        {
            BeginWhenDemoScriptsAreAvailable(PendingOperation.RunWebGL);
        }

        static void RunWebGLCore()
        {
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
            BeginWhenDemoScriptsAreAvailable(PendingOperation.RunWindows);
        }

        static void RunWindowsCore()
        {
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

        static void CreateRuntimeHumanoidFixtures(GameObject humanoid)
        {
            EnsureAssetFolder(RuntimeFixtureRoot);
            AssetDatabase.DeleteAsset(RuntimeHumanoidPath);
            AssetDatabase.DeleteAsset(RuntimeRigPath);
            AssetDatabase.DeleteAsset(RuntimeProfilePath);
            AssetDatabase.DeleteAsset(RuntimeHumanoidBindingsPath);

            GameObject instance = PrefabUtility.InstantiatePrefab(humanoid)
                as GameObject;
            if (!instance)
                throw new InvalidOperationException(
                    "Could not instantiate the certification Humanoid fixture.");
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        ControllerPath);
                PrefabUtility.SaveAsPrefabAsset(instance, RuntimeHumanoidPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (!AssetDatabase.CopyAsset(RegressionRigPath, RuntimeRigPath)
                || !AssetDatabase.CopyAsset(
                    RegressionProfilePath, RuntimeProfilePath))
            {
                throw new InvalidOperationException(
                    "Could not create runtime certification rig resources.");
            }

            RagdollHumanoidBindingProfile semantic =
                ScriptableObject.CreateInstance<RagdollHumanoidBindingProfile>();
            SerializedObject serialized = new SerializedObject(semantic);
            SerializedProperty bindings = serialized.FindProperty("bindings");
            bindings.arraySize = 2;
            SetHumanoidBinding(
                bindings.GetArrayElementAtIndex(0),
                "Root",
                HumanBodyBones.Hips);
            SetHumanoidBinding(
                bindings.GetArrayElementAtIndex(1),
                "Child",
                HumanBodyBones.Spine);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(semantic, RuntimeHumanoidBindingsPath);
        }

        static void SetHumanoidBinding(
            SerializedProperty value,
            string ragdollBone,
            HumanBodyBones humanoidBone)
        {
            value.FindPropertyRelative("ragdollBone")
                .FindPropertyRelative("name").stringValue = ragdollBone;
            value.FindPropertyRelative("humanoidBone").intValue =
                (int)humanoidBone;
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
            EnsureAssetFolder(GeneratedRegressionRoot);
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
                string sourceScene = AssetDatabase.GUIDToAssetPath(guids[0]);
                string certifiedScene = GeneratedRegressionRoot + "/"
                    + names[sceneIndex] + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(certifiedScene))
                    AssetDatabase.DeleteAsset(certifiedScene);
                if (!AssetDatabase.CopyAsset(sourceScene, certifiedScene))
                    throw new InvalidOperationException(
                        "Could not create isolated certified scene '"
                        + certifiedScene + "'.");
                Scene scene = EditorSceneManager.OpenScene(
                    certifiedScene,
                    OpenSceneMode.Additive);
                bool assigned = false;
                try
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        Transform[] transforms = roots[rootIndex]
                            .GetComponentsInChildren<Transform>(true);
                        for (int transformIndex = 0;
                             transformIndex < transforms.Length;
                             transformIndex++)
                        {
                            MonoBehaviour[] behaviours = transforms[transformIndex]
                                .GetComponents<MonoBehaviour>();
                            for (int index = 0; index < behaviours.Length; index++)
                            {
                                MonoBehaviour behaviour = behaviours[index];
                                if (!behaviour)
                                {
                                    throw new InvalidOperationException(
                                        names[sceneIndex]
                                        + " contains a Missing Script at GameObject '"
                                        + GetHierarchyPath(transforms[transformIndex])
                                        + "', MonoBehaviour component index " + index
                                        + ". The scene cannot be certified until that "
                                        + "specific serialized reference is repaired.");
                                }
                                if (behaviour.GetType().FullName
                                    != RegressionRunnerTypeName)
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

        static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
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
                    new[] { GeneratedRegressionRoot });
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
                target = CertificationTargetName(target),
                output = output,
                development = true,
                allowDebugging = true,
                result = "Unknown",
                ownDiagnostics = Array.Empty<string>(),
                externalWarnings = Array.Empty<string>(),
                diagnostics = Array.Empty<BuildDiagnosticEntry>()
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
                entry.result = report.summary.result.ToString();
                CollectBuildDiagnostics(report, entry);
                if (entry.ownDiagnostics.Length > 0)
                {
                    entry.error = "Hairibar build diagnostics: "
                        + string.Join(" | ", entry.ownDiagnostics);
                    return entry;
                }
                if (report.summary.result != BuildResult.Succeeded)
                {
                    entry.error = report.summary.result.ToString();
                    return entry;
                }

                if (execute)
                {
                    ExecuteWindowsPlayer(output, outputRoot);
                }
                entry.outputExists = target == BuildTarget.WebGL
                    ? Directory.Exists(output)
                    : File.Exists(output)
                        || (target == BuildTarget.StandaloneOSX
                            && Directory.Exists(output));
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

        static void CollectBuildDiagnostics(BuildReport report, BuildEntry entry)
        {
            List<string> own = new List<string>();
            List<string> external = new List<string>();
            List<BuildDiagnosticEntry> diagnostics =
                new List<BuildDiagnosticEntry>();
            BuildStep[] steps = report.steps;
            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                BuildStepMessage[] messages = steps[stepIndex].messages;
                for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++)
                {
                    BuildStepMessage message = messages[messageIndex];
                    if (message.type != LogType.Warning
                        && message.type != LogType.Error
                        && message.type != LogType.Exception
                        && message.type != LogType.Assert) continue;
                    string content = message.content ?? string.Empty;
                    bool isHairibar = IsHairibarBuildDiagnostic(content);
                    (isHairibar ? own : external).Add(
                        message.type + ": " + content);
                    diagnostics.Add(new BuildDiagnosticEntry
                    {
                        severity = message.type.ToString(),
                        own = isHairibar,
                        message = content
                    });
                }
            }
            entry.ownDiagnostics = own.ToArray();
            entry.externalWarnings = external.ToArray();
            entry.diagnostics = diagnostics.ToArray();
        }

        static string CertificationTargetName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64: return "Windows64";
                case BuildTarget.StandaloneLinux64: return "Linux64";
                case BuildTarget.StandaloneOSX: return "macOS";
                case BuildTarget.WebGL: return "WebGL";
                default: return target.ToString();
            }
        }

        internal static bool IsHairibarBuildDiagnostic(string content)
        {
            content = content ?? string.Empty;
            return content.IndexOf(
                    "Hairibar.Ragdoll",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || content.IndexOf(
                    "com.hairibar.ragdoll",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void ExecuteWindowsPlayer(string executable, string outputRoot)
        {
            string playerDirectory = Path.GetDirectoryName(executable);
            string resultPath = Path.Combine(
                outputRoot,
                "windows-player-result.json");
            string profilerPath = ResolveArtifactPath(
                "HAIRIBAR_PROFILER_RESULTS", outputRoot,
                "profiler-results.json");
            string scenePath = ResolveArtifactPath(
                "HAIRIBAR_SCENE_RESULTS", outputRoot,
                "scene-results.json");
            if (File.Exists(resultPath)) File.Delete(resultPath);
            if (File.Exists(profilerPath)) File.Delete(profilerPath);
            if (File.Exists(scenePath)) File.Delete(scenePath);
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
            start.EnvironmentVariables["HAIRIBAR_PROFILER_RESULTS"] =
                profilerPath;
            start.EnvironmentVariables["HAIRIBAR_SCENE_RESULTS"] = scenePath;
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
            RequireValidArtifact(
                RagdollEvidenceKind.WindowsPlayerScenario, resultPath);
            RequireValidArtifact(RagdollEvidenceKind.ProfilerResult, profilerPath);
            RequireValidArtifact(RagdollEvidenceKind.SceneArtifact, scenePath);
        }

        static string ResolveArtifactPath(
            string environmentVariable,
            string outputRoot,
            string fileName)
        {
            string configured = Environment.GetEnvironmentVariable(
                environmentVariable);
            return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(outputRoot, fileName)
                : configured);
        }

        static void RequireValidArtifact(RagdollEvidenceKind kind, string path)
        {
            RagdollArtifactValidationResult validation =
                RagdollEvidenceArtifactValidators.Validate(
                    new RagdollEvidenceArtifact { kind = kind, path = path });
            if (!validation.IsValid)
                throw new InvalidDataException(
                    kind + " evidence is invalid: " + validation.Reason
                    + " (" + path + ").");
        }

        static void WriteDocumentationAudit(string outputRoot)
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(RagdollAnimator).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
                throw new InvalidOperationException(
                    "Cannot resolve the Hairibar package for documentation audit.");
            string root = Path.GetFullPath(package.resolvedPath);
            string documentPath = Path.Combine(root, "Documentation~",
                "Certification", "MIGRATION-PUPPETMASTER-CLOSURE.md");
            string[] requiredSymbols =
            {
                "MasterMappingWeight", "MasterPinWeight",
                "MasterMuscleWeight", "MasterMuscleDamper",
                "MasterMuscleDamperMultiplier", "MasterAlpha",
                "SetMuscleWeights", "OnPostInitialized", "OnRead", "OnWrite",
                "OnFixTransforms", "OnPostLateUpdate", "TargetAnimator",
                "TargetAnimation", "EffectiveUpdateMode",
                "PrepareManualSimulation", "CompleteManualSimulation",
                "Respawn", "TrySetMuscles", "TryReplaceMuscles",
                "StartAction", "CurrentRigidbody", "AddAdditionalPin",
                "RemoveAdditionalPin", "IRagdollIKSolver", "OnCollision",
                "OnCollisionImpulse", "SetColliderSurfaceState",
                "FormerlySerializedAs", "State", "Mode", "IsActive",
                "IsBlending", "IsSwitchingMode", "Initiated"
            };
            bool audited = File.Exists(documentPath);
            string documentation = audited
                ? File.ReadAllText(documentPath)
                : string.Empty;
            if (audited)
            {
                for (int index = 0; index < requiredSymbols.Length; index++)
                {
                    string symbol = requiredSymbols[index];
                    audited &= documentation.IndexOf(symbol,
                        StringComparison.Ordinal) >= 0;
                }
                audited &= documentation.IndexOf(
                    "root-motion.com/puppetmasterdox",
                    StringComparison.OrdinalIgnoreCase) >= 0;
                audited &= documentation.IndexOf(
                    "docs.unity3d.com",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            }
            DocumentationMemberMapping[] memberMappings;
            audited &= BuildDocumentationMemberMappings(
                root, documentation, out memberMappings);
            var manifest = new DocumentationAuditManifest
            {
                succeeded = audited,
                documentPath = documentPath,
                documentSha256 = audited
                    ? RagdollClosurePipeline.ComputeSha256(documentPath)
                    : string.Empty,
                entries = new[]
                {
                    new DocumentationAuditEntry
                    {
                        id = "J07",
                        sourceUrl =
                            "http://www.root-motion.com/puppetmasterdox/html/pages.html",
                        affectedApi = string.Join(", ", requiredSymbols),
                        exactTest =
                            "Hairibar.Ragdoll.Animation.Editor.Tests."
                            + "RagdollEvidenceArtifactValidatorsTests."
                            + "J07_DocumentationAuditRequiresOfficialSourceAndExecutableMapping",
                        artifactKinds = new[]
                        {
                            nameof(RagdollEvidenceKind.DocumentationAudit),
                            nameof(RagdollEvidenceKind.NUnitEditMode)
                        },
                        audited = audited,
                        members = memberMappings
                    }
                }
            };
            string path = ResolveArtifactPath(
                "HAIRIBAR_DOCUMENTATION_AUDIT", outputRoot,
                "documentation-audit.json");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
            RequireValidArtifact(RagdollEvidenceKind.DocumentationAudit, path);
        }

        static bool BuildDocumentationMemberMappings(
            string packageRoot,
            string documentation,
            out DocumentationMemberMapping[] mappings)
        {
            var result = new List<DocumentationMemberMapping>();
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollAnimator), "Master authority", new[]
            {
                "MasterMappingWeight", "MasterPinWeight",
                "MasterMuscleWeight", "MasterMuscleDamper",
                "MasterMuscleDamperMultiplier", "MasterAlpha",
                "SetMuscleWeights"
            });
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollAnimator), "Lifecycle and animation", new[]
            {
                "OnPostInitialized", "OnRead", "OnWrite",
                "OnFixTransforms", "OnPostLateUpdate", "TargetAnimator",
                "TargetAnimation", "EffectiveUpdateMode",
                "PrepareManualSimulation", "CompleteManualSimulation",
                "Mode", "IsActive",
                "IsBlending", "IsSwitchingMode", "Initiated"
            });
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollPuppetBehaviour), "Lifecycle and animation", new[]
            {
                "State", "Respawn", "SetColliderSurfaceState",
                "OnCollision", "OnCollisionImpulse"
            });
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollAnimator), "Runtime hierarchy", new[]
            {
                "TrySetMuscles", "TryReplaceMuscles"
            });
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollProp), "Props and IK", new[]
            {
                "CurrentRigidbody", "AddAdditionalPin", "RemoveAdditionalPin"
            });
            AddMemberMappings(result, packageRoot, documentation,
                typeof(RagdollPropMelee), "Props and IK",
                new[] { "StartAction" });
            AddTypeMapping(result, packageRoot, documentation,
                typeof(IRagdollIKSolver), "Props and IK");

            System.Reflection.FieldInfo[] fields =
                typeof(RagdollPuppetBehaviour).GetFields(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.FieldInfo migrationField = null;
            for (int index = 0; index < fields.Length; index++)
                if (fields[index].IsDefined(
                    typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute),
                    true))
                {
                    migrationField = fields[index];
                    break;
                }
            string migrationSource = migrationField != null
                ? FindMemberSource(packageRoot,
                    typeof(RagdollPuppetBehaviour), "FormerlySerializedAs")
                : string.Empty;
            result.Add(new DocumentationMemberMapping
            {
                symbol = "FormerlySerializedAs",
                declaringType = typeof(RagdollPuppetBehaviour).FullName,
                memberName = migrationField?.Name ?? string.Empty,
                memberKind = "Attribute",
                documentationSection = "Master authority",
                sourcePath = migrationSource,
                verified = migrationField != null
                    && File.Exists(migrationSource)
                    && HasDocumentationMapping(
                        documentation, "Master authority",
                        "FormerlySerializedAs")
            });
            mappings = result.ToArray();
            for (int index = 0; index < mappings.Length; index++)
                if (!mappings[index].verified) return false;
            return true;
        }

        static void AddMemberMappings(
            List<DocumentationMemberMapping> result,
            string packageRoot,
            string documentation,
            Type type,
            string section,
            string[] names)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public;
            for (int index = 0; index < names.Length; index++)
            {
                System.Reflection.MemberInfo[] members =
                    type.GetMember(names[index], Flags);
                string source = members.Length != 0
                    ? FindMemberSource(packageRoot, type, names[index])
                    : string.Empty;
                result.Add(new DocumentationMemberMapping
                {
                    symbol = names[index],
                    declaringType = type.FullName,
                    memberName = names[index],
                    memberKind = members.Length != 0
                        ? members[0].MemberType.ToString()
                        : string.Empty,
                    documentationSection = section,
                    sourcePath = source,
                    verified = members.Length != 0 && File.Exists(source)
                        && HasDocumentationMapping(
                            documentation, section, names[index])
                });
            }
        }

        static void AddTypeMapping(
            List<DocumentationMemberMapping> result,
            string packageRoot,
            string documentation,
            Type type,
            string section)
        {
            string source = FindMemberSource(packageRoot, type, type.Name);
            result.Add(new DocumentationMemberMapping
            {
                symbol = type.Name,
                declaringType = type.FullName,
                memberName = type.Name,
                memberKind = "Type",
                documentationSection = section,
                sourcePath = source,
                verified = type.IsInterface && File.Exists(source)
                    && HasDocumentationMapping(documentation, section, type.Name)
            });
        }

        static bool HasDocumentationMapping(
            string documentation,
            string section,
            string symbol)
        {
            return documentation.IndexOf(
                    "## " + section, StringComparison.Ordinal) >= 0
                && documentation.IndexOf(symbol, StringComparison.Ordinal) >= 0;
        }

        static string FindMemberSource(
            string packageRoot,
            Type declaringType,
            string symbol)
        {
            string runtime = Path.Combine(packageRoot, "Animation", "Runtime");
            if (!Directory.Exists(runtime)) return string.Empty;
            string[] files = Directory.GetFiles(
                runtime, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
            {
                string content = File.ReadAllText(files[index]);
                string declaration = declaringType.IsInterface
                    ? "interface " + declaringType.Name
                    : "class " + declaringType.Name;
                if (content.IndexOf(declaration,
                        StringComparison.Ordinal) < 0) continue;
                if (content.IndexOf(symbol, StringComparison.Ordinal) >= 0)
                    return files[index];
            }
            return string.Empty;
        }

        [Serializable]
        sealed class PlayerResult
        {
            public bool succeeded = false;
        }
    }
}
