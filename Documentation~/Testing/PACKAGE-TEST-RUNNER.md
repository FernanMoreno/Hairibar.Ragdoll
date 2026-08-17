# Hairibar package test runner

This repository is a Unity package, not a standalone Unity project. Its
EditMode and PlayMode package tests must therefore be executed by a separate
Unity host project that resolves this checkout as a local package.

## Host project setup

Use a clean Unity host compatible with the package's `unity` field in
`package.json` (currently `6000.0` or newer). In the host project's
`Packages/manifest.json`, add the local package and enable its tests:

```json
{
  "dependencies": {
    "com.hairibar.ragdoll": "file:../../../Hairibar.Ragdoll",
    "com.unity.test-framework": "1.7.0"
  },
  "testables": ["com.hairibar.ragdoll"]
}
```

The relative path is resolved from the host's `Packages/` directory. For the
workspace layout used here (`Game/Unity Game/My project` beside
`Game/Hairibar.Ragdoll`), `file:../../../Hairibar.Ragdoll` is the correct path.
Adjust it only when the host is stored elsewhere. The package must resolve to
the repository root containing this file and `package.json`, not to a copied
`Library/ScriptAssemblies` directory.

After Unity finishes package resolution and compilation, the Test Runner must
list these assemblies:

- `Hairibar.Ragdoll.Animation.Tests`
- `Hairibar.Ragdoll.RagdollLab.Tests`
- `Hairibar.Ragdoll.Animation.Editor.Tests`

The runtime test assembly definitions intentionally use
`optionalUnityReferences: ["TestAssemblies"]` and have no `Editor` platform
restriction. This is the standard Unity custom-package test layout.

## Required verification

Assemblies under `Tests/Editor` are requested with `EditMode`. Assemblies
under `Tests/Runtime` are requested with `PlayMode`; requesting a runtime
assembly as EditMode can return `total=0` even when its DLL and reflected test
types are present.

The Stagger PlayMode tests provision their deterministic `StepRecovery`
controller and clips under a dedicated generated host fixture when the
resource is absent. This is package test infrastructure only: it does not read
or modify CODE RED assets. Provisioning validates the layer, directional
states, state motions, and the integer `StepSwingFoot` parameter.

Run the package host's EditMode tests and require a real result with
`total > 0`. At minimum, execute:

- `RagdollBipedBalancerMathTests.E03_PublicSettingsHaveObservableEffects`
- `RagdollLabAnalyzerTests.BalanceTelemetryPreservesStatesAndBuildsStaggerEpisode`
- `RagdollLabComparisonTests.MatchingRecoverablePushAcceptsSafeStabilityImprovement`

Then execute the physical package tests in PlayMode, including:

- `RagdollBipedStaggerBehaviourPlayModeTests.E02_PhysicalPush_StaggerRecoveryBenchmarkProvesCompleteEpisode`
- `RagdollBipedBalancerClosedLoopPlayModeTests.ClosedLoopBalancer_ReportsPairedOffOnMetrics`

Do not treat reflection, a loaded DLL, a missing fixture, a successful request
with `total=0`, or the synthetic `CODE RED` result as test execution evidence.

## Batch-mode alternative

The same host project can be run without the editor UI using Unity's test
command-line interface:

```powershell
Unity.exe -runTests -batchmode `
  -projectPath C:\path\to\HairibarPackageTestHost `
  -testResults C:\temp\hairibar-package-tests.xml `
  -testPlatform EditMode
```

Repeat with `-testPlatform PlayMode` for the physical tests. Inspect the XML
summary and fail the gate when the selected package tests are absent or when
`total` is zero.

## Current environment result

The connected Unity MCP host is named `My project`; it resolves
`com.hairibar.ragdoll` from this checkout and declares it testable. Runtime
package tests are valid when requested in PlayMode: the RagdollLab runtime
assembly recently returned `53/53`, the complete Stagger class `9/9`, and the
fixture/E02 regression set `6/6`, with no compiler errors. An earlier EditMode
request returned only the synthetic host entry with `total=0`; that was a
runner-mode mismatch and must not be counted as execution evidence.

Feature 010 focused evidence on the same host returned real non-zero totals:
the catalog EditMode selector `3/3`, the Balancer matrix `4/4`, the Stagger
actuator/routing/E02/state-machine selector `7/7`, and the Balancer math
selectors `23/23`. The matrix covered three support widths in one direction
and the nominal width in the opposite direction; it remains bounded fixture
evidence rather than a universal Balancer claim.

References:

- Unity custom package tests: https://docs.unity3d.com/Manual/cus-tests.html
- Unity test command line: https://docs.unity3d.com/Manual/test-framework/run-tests-from-command-line.html
- Unity command-line test reference: https://docs.unity3d.com/Manual/test-framework/reference-command-line.html
