using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    /// <summary>
    /// Materializes strict closure inputs from artifacts produced by separate
    /// Unity CLI processes. It never manufactures a successful artifact when a
    /// producer did not run.
    /// </summary>
    internal static class RagdollClosureCoordinator
    {
        internal const string OutputEnvironmentVariable =
            "HAIRIBAR_CLOSURE_OUTPUT";
        internal const string RunIdEnvironmentVariable =
            "HAIRIBAR_CLOSURE_RUN_ID";

        [Serializable]
        internal sealed class RunContext
        {
            public int schemaVersion = 1;
            public string certificationRunId;
            public string sourceRevision;
            public string sourceTreeSha256;
            public string sourceLatestWriteUtc;
            public string createdUtc;
        }

        [Serializable]
        sealed class EmbeddedProvenance
        {
            public string certificationRunId;
            public string sourceRevision;
            public string sourceTreeSha256;
        }

        internal static RunContext EnsureRunContext()
        {
            string output = RequireOutputRoot();
            string path = Path.Combine(output, "run-context.json");
            string sourceHash = RagdollCoverageManifest
                .ComputeCurrentSourceTreeSha256();
            string requestedRunId = Environment.GetEnvironmentVariable(
                RunIdEnvironmentVariable);
            Guid parsed;
            if (!Guid.TryParse(requestedRunId, out parsed))
                throw new InvalidOperationException(
                    RunIdEnvironmentVariable + " must contain a GUID.");
            if (File.Exists(path))
            {
                RunContext existing = JsonUtility.FromJson<RunContext>(
                    File.ReadAllText(path));
                ValidateRunContext(existing, requestedRunId, sourceHash);
                return existing;
            }
            var context = new RunContext
            {
                certificationRunId = requestedRunId,
                sourceTreeSha256 = sourceHash,
                sourceRevision = ResolveSourceRevision(sourceHash),
                sourceLatestWriteUtc = RagdollCoverageManifest
                    .CurrentSourceLatestWriteUtc(),
                createdUtc = DateTime.UtcNow.ToString("O")
            };
            File.WriteAllText(path, JsonUtility.ToJson(context, true));
            return context;
        }

        internal static RunContext ReadRunContext()
        {
            string path = Path.Combine(RequireOutputRoot(), "run-context.json");
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "The closure run context has not been produced.", path);
            RunContext context = JsonUtility.FromJson<RunContext>(
                File.ReadAllText(path));
            ValidateRunContext(
                context,
                Environment.GetEnvironmentVariable(RunIdEnvironmentVariable),
                RagdollCoverageManifest.ComputeCurrentSourceTreeSha256());
            return context;
        }

        internal static string GenerateProvisional()
        {
            string output = RequireOutputRoot();
            RunContext context = ReadRunContext();
            string sourceHash = context.sourceTreeSha256;
            string revision = context.sourceRevision;
            var artifacts = new List<RagdollEvidenceArtifact>();
            var nunit = new List<string>();

            AddNUnit(
                "HAIRIBAR_EDITMODE_RESULTS",
                RagdollEvidenceKind.NUnitEditMode,
                "Editor",
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts,
                nunit);
            AddNUnit(
                "HAIRIBAR_PLAYMODE_RESULTS",
                RagdollEvidenceKind.NUnitPlayMode,
                "Editor",
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts,
                nunit);

            AddIfPresent(
                Path.Combine(output, "build-manifest.json"),
                RagdollEvidenceKind.BuildReport,
                "MultiPlatform",
                "DevelopmentBuilds",
                new[] { "J01" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);
            AddIfPresent(
                Path.Combine(output, "windows-player-result.json"),
                RagdollEvidenceKind.WindowsPlayerScenario,
                "Windows64",
                "RegressionScenes",
                new[] { "H05", "H08", "J04", "J05" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_LINUX_PLAYER_RESULTS",
                RagdollEvidenceKind.LinuxPlayerScenario,
                "Linux64",
                "RegressionScenes",
                new[] { "H08", "J04" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_PROFILER_RESULTS",
                RagdollEvidenceKind.ProfilerResult,
                "Player",
                "CriticalPaths",
                new[] { "H05", "H08", "I07", "J05" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_SCENE_RESULTS",
                RagdollEvidenceKind.SceneArtifact,
                "Player",
                "RegressionScenes",
                new[] { "J04" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_DOCUMENTATION_AUDIT",
                RagdollEvidenceKind.DocumentationAudit,
                "Editor",
                "ApiMigration",
                new[] { "J07" },
                revision,
                sourceHash,
                context.certificationRunId,
                artifacts);

            var request = new RagdollCoverageRequest
            {
                nunitResultPaths = nunit.ToArray(),
                artifacts = artifacts.ToArray(),
                sourceRevision = revision,
                sourceTreeSha256 = sourceHash,
                sourceLatestWriteUtc = context.sourceLatestWriteUtc,
                certificationRunId = context.certificationRunId
            };
            RagdollCoverageManifest.Manifest manifest =
                RagdollCoverageManifest.Build(request);
            return RagdollClosurePipeline.WriteProvisional(
                manifest,
                Path.Combine(output, "coverage-manifest-provisional.json"));
        }

        internal static RagdollClosurePipeline.IndependentValidation
            ValidateProvisional()
        {
            string output = RequireOutputRoot();
            return RagdollClosurePipeline.ValidateProvisional(
                Path.Combine(output, "coverage-manifest-provisional.json"),
                Path.Combine(output, "coverage-manifest-validation.json"));
        }

        internal static RagdollClosurePipeline.FinalManifestEnvelope
            FinalizeClosure()
        {
            string output = RequireOutputRoot();
            return RagdollClosurePipeline.FinalizeManifest(
                Path.Combine(output, "coverage-manifest-provisional.json"),
                Path.Combine(output, "coverage-manifest-validation.json"),
                Path.Combine(output, "coverage-manifest-final.json"));
        }

        static void AddNUnit(
            string environmentVariable,
            RagdollEvidenceKind kind,
            string platform,
            string revision,
            string sourceHash,
            string certificationRunId,
            List<RagdollEvidenceArtifact> artifacts,
            List<string> nunit)
        {
            string path = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(path)) return;
            path = Path.GetFullPath(path);
            RunContext context = ReadRunContext();
            DateTime createdUtc;
            if (!DateTime.TryParse(context.createdUtc, out createdUtc)
                || !File.Exists(path)
                || File.GetLastWriteTimeUtc(path) < createdUtc.ToUniversalTime())
                throw new InvalidDataException(
                    kind + " NUnit artifact was not produced during closure run "
                    + certificationRunId + ".");
            nunit.Add(path);
            AddIfPresent(
                path, kind, platform, environmentVariable,
                Array.Empty<string>(), revision, sourceHash,
                certificationRunId, artifacts);
        }

        static void AddOptionalEnvironmentArtifact(
            string variable,
            RagdollEvidenceKind kind,
            string platform,
            string scenario,
            string[] ids,
            string revision,
            string sourceHash,
            string certificationRunId,
            List<RagdollEvidenceArtifact> artifacts)
        {
            string path = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(path))
                AddIfPresent(
                    path, kind, platform, scenario, ids,
                    revision, sourceHash, certificationRunId, artifacts);
        }

        static void AddIfPresent(
            string path,
            RagdollEvidenceKind kind,
            string platform,
            string scenario,
            string[] ids,
            string revision,
            string sourceHash,
            string certificationRunId,
            List<RagdollEvidenceArtifact> artifacts)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) return;
            if (kind != RagdollEvidenceKind.NUnitEditMode
                && kind != RagdollEvidenceKind.NUnitPlayMode)
            {
                EmbeddedProvenance embedded = JsonUtility.FromJson<
                    EmbeddedProvenance>(File.ReadAllText(full));
                if (embedded == null
                    || !string.Equals(embedded.certificationRunId,
                        certificationRunId, StringComparison.Ordinal)
                    || !string.Equals(embedded.sourceRevision, revision,
                        StringComparison.Ordinal)
                    || !string.Equals(embedded.sourceTreeSha256, sourceHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        kind + " artifact does not embed this closure run's "
                        + "source provenance: " + full);
            }
            artifacts.Add(new RagdollEvidenceArtifact
            {
                kind = kind,
                path = full,
                sha256 = RagdollClosurePipeline.ComputeSha256(full),
                platform = platform,
                scenario = scenario,
                generatedUtc = File.GetLastWriteTimeUtc(full).ToString("O"),
                certificationRunId = certificationRunId,
                sourceRevision = revision,
                sourceTreeSha256 = sourceHash,
                capabilityIds = ids ?? Array.Empty<string>()
            });
        }

        static string RequireOutputRoot()
        {
            string output = Environment.GetEnvironmentVariable(
                OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    OutputEnvironmentVariable + " must point to the closure output directory.");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            return output;
        }

        internal static string ResolveSourceRevision(string sourceHash)
        {
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(RagdollAnimator).Assembly);
            string packageRoot = package?.resolvedPath;
            if (string.IsNullOrWhiteSpace(packageRoot))
                return "tree-" + sourceHash;
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "-C \"" + packageRoot + "\" rev-parse HEAD",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(start))
                {
                    if (process == null) return "tree-" + sourceHash;
                    string head = process.StandardOutput.ReadToEnd().Trim();
                    if (!process.WaitForExit(10000) || process.ExitCode != 0
                        || string.IsNullOrWhiteSpace(head))
                        return "tree-" + sourceHash;
                    return head + "-tree-" + sourceHash;
                }
            }
            catch
            {
                return "tree-" + sourceHash;
            }
        }

        static void ValidateRunContext(
            RunContext context,
            string expectedRunId,
            string expectedSourceHash)
        {
            if (context == null || context.schemaVersion != 1)
                throw new InvalidDataException("Closure run-context schema is invalid.");
            if (!string.Equals(context.certificationRunId, expectedRunId,
                    StringComparison.Ordinal)
                || !string.Equals(context.sourceTreeSha256,
                    expectedSourceHash, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(context.sourceRevision)
                || !context.sourceRevision.EndsWith(
                    "tree-" + expectedSourceHash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Closure run-context provenance does not match this process/source.");
        }
    }
}
