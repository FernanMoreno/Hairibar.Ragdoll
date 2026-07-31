# Implementation evidence ledger

This ledger records the public contract used for the remediation. It is not a
parity certificate. Hairibar code is original; RootMotion documentation defines
observable capability, while Unity documentation defines engine mechanics.

## Normative public sources

- RootMotion Support, PuppetMaster FAQ and manual-simulation order:
  https://rootmotion.freshdesk.com/support/solutions/articles/77000057786-faq
- RootMotion Support, PuppetMaster learning resources:
  https://rootmotion.freshdesk.com/support/solutions
- Unity 6 `SimulationMode.Script`:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SimulationMode.Script.html
- Unity 6 `PlayableGraph.SetTimeUpdateMode`:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Playables.PlayableGraph.SetTimeUpdateMode.html
- Unity 6 Legacy Animation component and Animate Physics:
  https://docs.unity3d.com/6000.0/Documentation/Manual/class-Animation.html
- Unity 6 Animator state playback contract:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.Play.html
- Unity 6 undo-aware component creation:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Undo.AddComponent.html
- Unity 6 recording several existing objects for Undo:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Undo.RecordObjects.html
- Unity 6 common `Collider` properties preserved during shape conversion:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider.html

The detailed PuppetMaster capability inventory remains the 140-row historical
matrix in `PUPPETMASTER_COVERAGE_AUDIT.md`, derived from the official Doxygen
manual/reference distributed by RootMotion. Its current explicit status is recorded
in `PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md` (139 verified and one deliberate
N/A for Final IK). D46 is included: pressure, pressure-to-COM vector and its angle
are official behaviour requirements.

## Hairibar-owned design decisions

- Biometric regional percentages are Hairibar policy, normalized over included
  bones. They are not attributed to PuppetMaster.
- Center-of-pressure weighting uses collision impulse with a unit fallback for
  zero-impulse contacts. RootMotion defines the exposed concept, not this solver.
- Manual simulation names are `PrepareManualSimulation` and
  `CompleteManualSimulation`; their ordering mirrors RootMotion's documented
  pre-simulate / `Physics.Simulate` / post-simulate contract.
- Runtime setup rollback and aggregate respawn errors are Hairibar transaction
  semantics built on Unity object and physics contracts.
- Live authoring uses Unity's documented `Undo.AddComponent` path, whose undo
  operation destroys the newly added component; rebuild ownership remains Hairibar
  tooling policy.
- Complete Editor setup records existing hierarchies and project physics settings,
  while component and object creation uses Unity's Undo APIs. Transaction grouping
  and rollback boundaries are Hairibar tooling policy.

## Current verification snapshot

- PlayMode: 518 passing, including all three runtime setup paths, rollback,
  quadruped get-up event routing, collider-surface lifecycle restoration, and immediate
  respawn from Unpinned/GetUp/Dead/Frozen, post-initialization isolation, Legacy
  lifecycle restoration, manual simulation, and FixedUpdate add/replace/remove
  hierarchy transactions.
- EditMode: 33 passing, including live-authoring, complete-setup commit, collider
  conversion matrix, ownership, Undo/Redo, rollback and typed `BoneName` equality.
- Development certification: Windows64, Linux64, macOS and WebGL BuildReports pass.
  The Windows player executes 109 assertions in four integral regression scenes;
  after 120 warm-up and 600 measured frames its GC counter reports zero for mapping,
  matching, COM, additional pin and Baker Realtime.
- Performance instrumentation covers 1/10/25/50 puppets across Active tree,
  Active flat, Kinematic tree and Disabled tree, recording CPU/memory median and p95.
  Figures remain platform-local. Linux execution still requires a Linux host.
- `PUPPETMASTER-COVERAGE-FINAL-0050.md` records the restored certification and its
  explicit non-parity limits.
