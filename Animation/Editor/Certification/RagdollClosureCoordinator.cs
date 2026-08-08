using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.PackageManager;

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

        internal static string GenerateProvisional()
        {
            string output = RequireOutputRoot();
            string sourceHash = RagdollCoverageManifest
                .ComputeCurrentSourceTreeSha256();
            string revision = ResolveSourceRevision(sourceHash);
            var artifacts = new List<RagdollEvidenceArtifact>();
            var nunit = new List<string>();

            AddNUnit(
                "HAIRIBAR_EDITMODE_RESULTS",
                RagdollEvidenceKind.NUnitEditMode,
                "Editor",
                revision,
                sourceHash,
                artifacts,
                nunit);
            AddNUnit(
                "HAIRIBAR_PLAYMODE_RESULTS",
                RagdollEvidenceKind.NUnitPlayMode,
                "Editor",
                revision,
                sourceHash,
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
                artifacts);
            AddIfPresent(
                Path.Combine(output, "windows-player-result.json"),
                RagdollEvidenceKind.WindowsPlayerScenario,
                "Windows64",
                "RegressionScenes",
                new[] { "H08", "J04" },
                revision,
                sourceHash,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_LINUX_PLAYER_RESULTS",
                RagdollEvidenceKind.LinuxPlayerScenario,
                "Linux64",
                "RegressionScenes",
                new[] { "H08", "J04" },
                revision,
                sourceHash,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_PROFILER_RESULTS",
                RagdollEvidenceKind.ProfilerResult,
                "Player",
                "CriticalPaths",
                new[] { "H08", "J05" },
                revision,
                sourceHash,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_SCENE_RESULTS",
                RagdollEvidenceKind.SceneArtifact,
                "Player",
                "RegressionScenes",
                new[] { "J04" },
                revision,
                sourceHash,
                artifacts);
            AddOptionalEnvironmentArtifact(
                "HAIRIBAR_DOCUMENTATION_AUDIT",
                RagdollEvidenceKind.DocumentationAudit,
                "Editor",
                "ApiMigration",
                new[] { "J07" },
                revision,
                sourceHash,
                artifacts);

            var request = new RagdollCoverageRequest
            {
                nunitResultPaths = nunit.ToArray(),
                artifacts = artifacts.ToArray(),
                sourceRevision = revision,
                sourceTreeSha256 = sourceHash,
                sourceLatestWriteUtc = RagdollCoverageManifest
                    .CurrentSourceLatestWriteUtc()
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
            List<RagdollEvidenceArtifact> artifacts,
            List<string> nunit)
        {
            string path = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(path)) return;
            path = Path.GetFullPath(path);
            nunit.Add(path);
            AddIfPresent(
                path, kind, platform, environmentVariable,
                Array.Empty<string>(), revision, sourceHash, artifacts);
        }

        static void AddOptionalEnvironmentArtifact(
            string variable,
            RagdollEvidenceKind kind,
            string platform,
            string scenario,
            string[] ids,
            string revision,
            string sourceHash,
            List<RagdollEvidenceArtifact> artifacts)
        {
            string path = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(path))
                AddIfPresent(
                    path, kind, platform, scenario, ids,
                    revision, sourceHash, artifacts);
        }

        static void AddIfPresent(
            string path,
            RagdollEvidenceKind kind,
            string platform,
            string scenario,
            string[] ids,
            string revision,
            string sourceHash,
            List<RagdollEvidenceArtifact> artifacts)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) return;
            artifacts.Add(new RagdollEvidenceArtifact
            {
                kind = kind,
                path = full,
                sha256 = RagdollClosurePipeline.ComputeSha256(full),
                platform = platform,
                scenario = scenario,
                generatedUtc = File.GetLastWriteTimeUtc(full).ToString("O"),
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

        static string ResolveSourceRevision(string sourceHash)
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
    }
}
