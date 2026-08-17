#!/usr/bin/env python3
"""Run and verify one CODE RED RagdollLab baseline/candidate pair.

The Unity project owns capture and manifest publication. This launcher owns the
asynchronous process boundary: it starts two isolated PlayMode runs, stages the
baseline evaluation for the candidate comparison, and fails closed unless both
run directories prove the requested binding and SHA-256 payloads.

It then invokes the package planner through a third Unity Editor process and
persists the formal decision. Planner promotion remains a separate operation
and must not be inferred from process exit codes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import struct
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


PACKAGE_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CODE_RED = PACKAGE_ROOT.parent / "Unity Game" / "CODE RED"
DEFAULT_UNITY = Path("/mnt/c/Program Files/Unity/Hub/Editor/6000.5.2f1/Editor/Unity.exe")
DEFAULT_PREFAB = "Assets/_Project/Prefabs/Enemies/PF_Enemy_Grunt_HairibarPrototype.prefab"
DEFAULT_SCENE = "Assets/_Project/Scenes/TestScenes/Benchmark_Recovery.unity"
DEFAULT_PARAMETER = "staggerDuration"
DEFAULT_BASELINE = 0.55
DEFAULT_CANDIDATE = 0.65
UNITY_METHOD = "CodeRed.RagdollLab.Editor.RagdollLabBatch.RunAll"


class EvidenceError(RuntimeError):
    pass


@dataclass(frozen=True)
class RunSpec:
    role: str
    run_id: str
    value: float
    configuration_fingerprint: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session-id", required=True)
    parser.add_argument("--experiment-id", required=True)
    parser.add_argument("--artifact-root", type=Path, required=True)
    parser.add_argument("--baseline-run-id", default="baseline-001")
    parser.add_argument("--candidate-run-id", default="candidate-001")
    parser.add_argument("--parameter", default=DEFAULT_PARAMETER)
    parser.add_argument("--baseline-value", type=float, default=DEFAULT_BASELINE)
    parser.add_argument("--candidate-value", type=float, default=DEFAULT_CANDIDATE)
    parser.add_argument("--unity-executable", type=Path, default=DEFAULT_UNITY)
    parser.add_argument("--project-path", type=Path, default=DEFAULT_CODE_RED)
    parser.add_argument("--scene", default=DEFAULT_SCENE)
    parser.add_argument("--prefab", default=DEFAULT_PREFAB)
    parser.add_argument("--scenario", default="GameplayHit")
    parser.add_argument("--seed", type=int, default=12017)
    parser.add_argument("--frames", type=int, default=300)
    parser.add_argument("--timeout-seconds", type=int, default=900)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--session-state", type=Path, help="persisted tuning-session.json path")
    parser.add_argument(
        "--use-persisted-baseline",
        action="store_true",
        help="read the current baseline value for --parameter from tuning-session.json",
    )
    action = parser.add_mutually_exclusive_group()
    action.add_argument(
        "--evaluate-existing",
        action="store_true",
        help="skip Unity capture and formally evaluate an existing paired artifact root",
    )
    action.add_argument(
        "--promote-existing",
        action="store_true",
        help="promote an accepted tuning-decision.json through the persisted session",
    )
    action.add_argument(
        "--rollback-existing",
        action="store_true",
        help="rollback an active persisted tuning decision",
    )
    return parser.parse_args()


def windows_path(path: Path) -> str:
    """Translate WSL /mnt/<drive>/ paths for the Windows Unity process."""
    raw = str(path.resolve())
    if len(raw) >= 7 and raw[:5].lower() == "/mnt/" and raw[6] == "/":
        return raw[5].upper() + ":" + raw[6:]
    return raw


def unity_float(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def csharp_roundtrip_float(value: float) -> str:
    """Match float.ToString("R", InvariantCulture) for protocol values."""
    single = unity_float(value)
    # The planner serializes a C# Single with the shortest decimal that parses
    # back to the same binary32 value. Python's fixed-precision ``g`` output is
    # sometimes longer (for example 0.550000012 instead of 0.55).
    for precision in range(1, 10):
        candidate = format(single, f".{precision}g")
        if unity_float(float(candidate)) == single:
            return candidate
    return format(single, ".9g")


def configuration_fingerprint(parameter: str, value: float) -> str:
    return f"{parameter}={csharp_roundtrip_float(value)}"


def run_directory(root: Path, run_id: str) -> Path:
    if not run_id or run_id in {".", ".."} or Path(run_id).name != run_id:
        raise EvidenceError(f"unsafe run id: {run_id!r}")
    return root / run_id


def session_state_path(args: argparse.Namespace) -> Path:
    return (args.session_state or (args.artifact_root.parent / "tuning-session.json")).resolve()


def use_persisted_baseline(args: argparse.Namespace) -> None:
    path = session_state_path(args)
    if not path.is_file():
        raise EvidenceError(f"persisted session state not found: {path}")
    state = load_json(path)
    if state.get("sessionId") != args.session_id:
        raise EvidenceError("persisted session identity mismatch")
    if state.get("scenarioProfile") != "Stagger":
        raise EvidenceError("persisted session profile is not Stagger")
    baseline = state.get("baseline")
    if not isinstance(baseline, list):
        raise EvidenceError("persisted session baseline is missing")
    for value in baseline:
        if isinstance(value, dict) and value.get("name") == args.parameter:
            try:
                args.baseline_value = float(value["value"])
            except (KeyError, TypeError, ValueError) as error:
                raise EvidenceError("persisted baseline value is invalid") from error
            return
    raise EvidenceError(f"persisted baseline does not contain parameter: {args.parameter}")


def command_for(args: argparse.Namespace, spec: RunSpec) -> list[str]:
    artifact_root = windows_path(args.artifact_root)
    return [
        # Python is running under WSL, so the executable itself must retain its
        # host path. Unity's project/artifact arguments below use Windows paths.
        str(args.unity_executable.resolve()),
        "-batchmode",
        "-nographics",
        "-projectPath",
        windows_path(args.project_path),
        "-executeMethod",
        UNITY_METHOD,
        "-ragdollScene",
        args.scene,
        "-ragdollPrefab",
        args.prefab,
        "-ragdollScenario",
        args.scenario,
        "-ragdollSeed",
        str(args.seed),
        "-ragdollFrames",
        str(args.frames),
        "-ragdollTuningSessionId",
        args.session_id,
        "-ragdollTuningExperimentId",
        args.experiment_id,
        "-ragdollTuningRunId",
        spec.run_id,
        "-ragdollTuningRunRole",
        spec.role,
        "-ragdollTuningConfigurationFingerprint",
        spec.configuration_fingerprint,
        "-ragdollTuningBaselineConfigurationFingerprint",
        configuration_fingerprint(args.parameter, args.baseline_value),
        "-ragdollTuningParameter",
        args.parameter,
        "-ragdollTuningValue",
        csharp_roundtrip_float(spec.value),
        "-ragdollTuningArtifactRoot",
        artifact_root,
        "-ragdollTuningSessionStatePath",
        windows_path(session_state_path(args)),
    ]


def decision_command_for(args: argparse.Namespace) -> list[str]:
    return [
        str(args.unity_executable.resolve()),
        "-batchmode",
        "-nographics",
        "-projectPath",
        windows_path(args.project_path),
        "-executeMethod",
        "CodeRed.RagdollLab.Editor.RagdollLabBatch.RunTuningDecision",
        "-ragdollTuningSessionId",
        args.session_id,
        "-ragdollTuningExperimentId",
        args.experiment_id,
        "-ragdollTuningArtifactRoot",
        windows_path(args.artifact_root),
        "-ragdollTuningScenarioProfile",
        "Stagger",
        "-ragdollTuningParameter",
        args.parameter,
        "-ragdollTuningBaselineValue",
        csharp_roundtrip_float(args.baseline_value),
        "-ragdollTuningCandidateValue",
        csharp_roundtrip_float(args.candidate_value),
        "-ragdollTuningBaselineRunId",
        args.baseline_run_id,
        "-ragdollTuningCandidateRunId",
        args.candidate_run_id,
        "-ragdollTuningSessionStatePath",
        windows_path(session_state_path(args)),
    ]


def state_mutation_command_for(args: argparse.Namespace, action: str) -> list[str]:
    method = (
        "CodeRed.RagdollLab.Editor.RagdollLabBatch.RunTuningPromotion"
        if action == "promote"
        else "CodeRed.RagdollLab.Editor.RagdollLabBatch.RunTuningRollback"
    )
    return [
        str(args.unity_executable.resolve()),
        "-batchmode",
        "-nographics",
        "-projectPath",
        windows_path(args.project_path),
        "-executeMethod",
        method,
        "-ragdollTuningSessionId",
        args.session_id,
        "-ragdollTuningExperimentId",
        args.experiment_id,
        "-ragdollTuningSessionStatePath",
        windows_path(session_state_path(args)),
        "-ragdollTuningDecisionPath",
        windows_path(args.artifact_root / "tuning-decision.json"),
    ]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise EvidenceError(f"invalid JSON {path}: {error}") from error
    if not isinstance(value, dict):
        raise EvidenceError(f"JSON object required: {path}")
    return value


def verify_run(root: Path, args: argparse.Namespace, spec: RunSpec) -> dict[str, Any]:
    directory = run_directory(root, spec.run_id)
    manifest_path = directory / "tuning-manifest.json"
    evaluation_path = directory / "evaluation.json"
    balance_path = directory / "balance-comparison.json"
    for path in (manifest_path, evaluation_path, balance_path):
        if not path.is_file():
            raise EvidenceError(f"required artifact missing: {path}")

    manifest = load_json(manifest_path)
    expected = {
        "schemaVersion": "1.0.0",
        "sessionId": args.session_id,
        "experimentId": args.experiment_id,
        "runId": spec.run_id,
        "runRole": spec.role,
        "configurationFingerprint": spec.configuration_fingerprint,
        "baselineConfigurationFingerprint": configuration_fingerprint(
            args.parameter, args.baseline_value
        ),
        "treatmentParameter": args.parameter,
        "treatmentValueAvailable": True,
    }
    for key, value in expected.items():
        if manifest.get(key) != value:
            raise EvidenceError(
                f"manifest mismatch {spec.run_id}: {key}={manifest.get(key)!r}; "
                f"expected {value!r}"
            )
    if abs(float(manifest.get("treatmentValue", float("nan"))) - spec.value) > 1e-6:
        raise EvidenceError(f"manifest treatment value mismatch: {spec.run_id}")
    if manifest.get("evaluationFile") != "evaluation.json":
        raise EvidenceError(f"unsafe evaluation filename: {spec.run_id}")
    if manifest.get("balanceComparisonFile") != "balance-comparison.json":
        raise EvidenceError(f"unsafe comparison filename: {spec.run_id}")
    if manifest.get("evaluationSha256") != sha256(evaluation_path):
        raise EvidenceError(f"evaluation hash mismatch: {spec.run_id}")
    if manifest.get("balanceComparisonSha256") != sha256(balance_path):
        raise EvidenceError(f"balance comparison hash mismatch: {spec.run_id}")

    evaluation = load_json(evaluation_path)
    metadata = evaluation.get("metadata")
    if not isinstance(metadata, dict):
        raise EvidenceError(f"evaluation metadata missing: {spec.run_id}")
    metadata_expectations = {
        "tuningSessionId": args.session_id,
        "experimentId": args.experiment_id,
        "runId": spec.run_id,
        "runRole": spec.role,
        "configurationFingerprint": spec.configuration_fingerprint,
        "baselineConfigurationFingerprint": configuration_fingerprint(
            args.parameter, args.baseline_value
        ),
        "treatmentParameter": args.parameter,
        "treatmentValueAvailable": True,
    }
    for key, value in metadata_expectations.items():
        if metadata.get(key) != value:
            raise EvidenceError(
                f"evaluation metadata mismatch {spec.run_id}: {key}"
            )
    if abs(float(metadata.get("treatmentValue", float("nan"))) - spec.value) > 1e-6:
        raise EvidenceError(f"evaluation treatment value mismatch: {spec.run_id}")
    if metadata.get("scenarioProfile") != "Stagger":
        raise EvidenceError(
            f"scenario profile is not Stagger for {spec.run_id}: "
            f"{metadata.get('scenarioProfile')!r}"
        )

    balance = load_json(balance_path)
    if spec.role == "candidate" and balance.get("setupMatched") is not True:
        raise EvidenceError(
            f"candidate comparison is not paired/setup-matched: {balance.get('invalidReason')!r}"
        )
    return {
        "runId": spec.run_id,
        "runRole": spec.role,
        "directory": str(directory),
        "evaluationSha256": sha256(evaluation_path),
        "balanceComparisonSha256": sha256(balance_path),
        "scenarioProfile": metadata.get("scenarioProfile"),
        "frameCount": evaluation.get("frameCount"),
        "balanceDecision": balance.get("decision"),
        "balanceSetupMatched": balance.get("setupMatched"),
        "balanceSafetyGuardsPassed": balance.get("safetyGuardsPassed"),
    }


def run_state_mutation(args: argparse.Namespace, action: str, root: Path) -> int:
    state_path = session_state_path(args)
    decision_path = root / "tuning-decision.json"
    if not state_path.is_file():
        raise EvidenceError(f"persisted session state not found: {state_path}")
    if not decision_path.is_file():
        raise EvidenceError(f"tuning decision not found: {decision_path}")
    if not args.unity_executable.is_file():
        raise EvidenceError(f"Unity executable not found: {args.unity_executable}")
    if not args.project_path.is_dir():
        raise EvidenceError(f"CODE RED project not found: {args.project_path}")

    log_path = root / f"launcher-{action}.log"
    command = state_mutation_command_for(args, action)
    print(f"[paired-tuning] {action} persisted candidate", flush=True)
    try:
        with log_path.open("w", encoding="utf-8") as log:
            completed = subprocess.run(
                command,
                cwd=args.project_path,
                stdout=log,
                stderr=subprocess.STDOUT,
                timeout=args.timeout_seconds,
                check=False,
            )
    except subprocess.TimeoutExpired as error:
        raise EvidenceError(f"Unity timeout for {action}; see {log_path}") from error
    if completed.returncode != 0:
        raise EvidenceError(f"Unity {action} failed with exit {completed.returncode}; see {log_path}")

    lifecycle_path = root / ("tuning-promotion.json" if action == "promote" else "tuning-rollback.json")
    if not lifecycle_path.is_file():
        raise EvidenceError(f"{action} exited successfully without {lifecycle_path.name}")
    lifecycle = load_json(lifecycle_path)
    decision = lifecycle.get("decision")
    if lifecycle.get("sessionId") != args.session_id or lifecycle.get("experimentId") != args.experiment_id:
        raise EvidenceError(f"{action} provenance mismatch")
    if lifecycle.get("action") != action or not isinstance(decision, dict):
        raise EvidenceError(f"{action} lifecycle payload is invalid")
    if action == "promote" and decision.get("decision") != "promoted":
        raise EvidenceError("promotion lifecycle did not reach promoted")
    if action == "rollback" and lifecycle.get("experimentState") != "rolled_back":
        raise EvidenceError("rollback lifecycle did not close the experiment")
    if lifecycle.get("sessionCandidateActive") is not False:
        raise EvidenceError(f"{action} lifecycle left a candidate active")

    summary = {
        "schemaVersion": "1.0.0",
        "sessionId": args.session_id,
        "experimentId": args.experiment_id,
        "action": action,
        "process": {"role": action, "exitCode": completed.returncode, "log": str(log_path)},
        "decisionPath": str(decision_path),
        "lifecyclePath": str(lifecycle_path),
        "sessionStatePath": str(state_path),
        "sessionStateSha256": sha256(state_path),
        "lifecycleSha256": sha256(lifecycle_path),
        "decision": decision,
        "baselineFingerprint": lifecycle.get("baselineFingerprint"),
        "baselineRunId": lifecycle.get("baselineRunId"),
        "decisionStatus": "promoted" if action == "promote" else "rolled-back",
    }
    summary_path = root / f"tuning-{action}.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0


def main() -> int:
    args = parse_args()
    if args.use_persisted_baseline and not args.promote_existing and not args.rollback_existing:
        use_persisted_baseline(args)
    if args.frames < 1 or args.timeout_seconds < 1:
        raise EvidenceError("frames and timeout must be positive")
    if args.baseline_value == args.candidate_value:
        raise EvidenceError("candidate must differ from baseline")
    baseline_fp = configuration_fingerprint(args.parameter, args.baseline_value)
    candidate_fp = configuration_fingerprint(args.parameter, args.candidate_value)
    specs = (
        RunSpec("baseline", args.baseline_run_id, args.baseline_value, baseline_fp),
        RunSpec("candidate", args.candidate_run_id, args.candidate_value, candidate_fp),
    )
    root = args.artifact_root.resolve()
    mutation = "promote" if args.promote_existing else "rollback" if args.rollback_existing else None
    if mutation:
        if args.dry_run:
            print(json.dumps({"action": mutation, "command": state_mutation_command_for(args, mutation)}, indent=2))
            return 0
        return run_state_mutation(args, mutation, root)
    if not args.evaluate_existing and any(run_directory(root, spec.run_id).exists() for spec in specs):
        raise EvidenceError(
            "artifact run directory already exists; choose fresh run IDs to avoid mixing evidence"
        )
    commands = [command_for(args, spec) for spec in specs]
    decision_command = decision_command_for(args)
    if args.dry_run:
        print(json.dumps({
            "baseline": commands[0],
            "candidate": commands[1],
            "decision": decision_command,
            "sessionState": str(session_state_path(args)),
        }, indent=2))
        return 0
    if not args.unity_executable.is_file():
        raise EvidenceError(f"Unity executable not found: {args.unity_executable}")
    if not args.project_path.is_dir():
        raise EvidenceError(f"CODE RED project not found: {args.project_path}")
    process_results: list[dict[str, Any]] = []
    if not args.evaluate_existing:
        root.mkdir(parents=True, exist_ok=False)
        for index, (spec, command) in enumerate(zip(specs, commands)):
            log_path = root / f"launcher-{spec.role}.log"
            print(f"[paired-tuning] launching {spec.role}: {spec.run_id}", flush=True)
            try:
                with log_path.open("w", encoding="utf-8") as log:
                    completed = subprocess.run(
                        command,
                        cwd=args.project_path,
                        stdout=log,
                        stderr=subprocess.STDOUT,
                        timeout=args.timeout_seconds,
                        check=False,
                    )
            except subprocess.TimeoutExpired as error:
                raise EvidenceError(f"Unity timeout for {spec.role}; see {log_path}") from error
            process_results.append(
                {"role": spec.role, "runId": spec.run_id, "exitCode": completed.returncode, "log": str(log_path)}
            )
            if completed.returncode != 0:
                raise EvidenceError(
                    f"Unity failed for {spec.role} with exit {completed.returncode}; see {log_path}"
                )
            if index == 0:
                baseline_evaluation = run_directory(root, spec.run_id) / "evaluation.json"
                if not baseline_evaluation.is_file():
                    raise EvidenceError("baseline exited successfully without evaluation.json")
                candidate_directory = run_directory(root, specs[1].run_id)
                candidate_directory.mkdir()
                # Candidate recorder uses the prior evaluation in its own output
                # directory to build the authoritative paired comparison.
                shutil.copy2(baseline_evaluation, candidate_directory / "evaluation.json")
    elif not root.is_dir():
        raise EvidenceError(f"existing artifact root not found: {root}")

    reports = [verify_run(root, args, spec) for spec in specs]
    decision_log = root / "launcher-decision.log"
    print("[paired-tuning] evaluating persisted pair through planner", flush=True)
    try:
        with decision_log.open("w", encoding="utf-8") as log:
            decision_process = subprocess.run(
                decision_command,
                cwd=args.project_path,
                stdout=log,
                stderr=subprocess.STDOUT,
                timeout=args.timeout_seconds,
                check=False,
            )
    except subprocess.TimeoutExpired as error:
        raise EvidenceError(f"Unity timeout for planner decision; see {decision_log}") from error
    process_results.append(
        {"role": "planner", "runId": None, "exitCode": decision_process.returncode, "log": str(decision_log)}
    )
    if decision_process.returncode != 0:
        raise EvidenceError(
            f"planner decision failed with exit {decision_process.returncode}; see {decision_log}"
        )
    decision_path = root / "tuning-decision.json"
    if not decision_path.is_file():
        raise EvidenceError("planner exited successfully without tuning-decision.json")
    decision = load_json(decision_path)
    if decision.get("sessionId") != args.session_id or decision.get("experimentId") != args.experiment_id:
        raise EvidenceError("planner decision provenance mismatch")
    if decision.get("persistedPair") is not True:
        raise EvidenceError("planner did not report persistedPair=true")
    planner_decision = decision.get("decision")
    if not isinstance(planner_decision, dict) or planner_decision.get("decision") not in {
        "accepted", "neutral", "rejected", "invalid"
    }:
        raise EvidenceError("planner decision payload is invalid")
    state_path = session_state_path(args)
    if not state_path.is_file():
        raise EvidenceError("planner exited successfully without tuning-session.json")
    if decision.get("sessionStatePath") != windows_path(state_path):
        raise EvidenceError("planner session state provenance mismatch")
    summary = {
        "schemaVersion": "1.0.0",
        "sessionId": args.session_id,
        "experimentId": args.experiment_id,
        "scenario": args.scenario,
        "scenarioProfile": "Stagger",
        "parameter": args.parameter,
        "baselineValue": unity_float(args.baseline_value),
        "candidateValue": unity_float(args.candidate_value),
        "baselineConfigurationFingerprint": baseline_fp,
        "candidateConfigurationFingerprint": candidate_fp,
        "processes": process_results,
        "runs": reports,
        "plannerDecisionPath": str(decision_path),
        "sessionStatePath": str(state_path),
        "sessionStateSha256": sha256(state_path),
        "plannerDecision": planner_decision,
        "evidenceStatus": "verified-paired-artifacts",
        "decisionStatus": "planner-" + planner_decision["decision"],
    }
    summary_path = root / "paired-run.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except EvidenceError as error:
        print(f"[paired-tuning] FAIL CLOSED: {error}", file=sys.stderr)
        raise SystemExit(2)
