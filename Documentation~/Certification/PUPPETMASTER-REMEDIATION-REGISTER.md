# PuppetMaster remediation register

Baseline: `e68e9e4076a9eafd864809730278f1d0e90d6b33`, captured 2026-07-31.
The working tree already contained 125 modified or untracked entries before this
remediation pass. They are preserved and are not an authorization boundary for
unrelated rewrites.

Only the public RootMotion manual/reference linked from RootMotion Support and the
official Unity 6 documentation are normative. A row may be closed only by runtime
behaviour plus a regression test; symbol presence or compilation alone is not enough.

| ID | Severity | Contract | Affected area | Required proof | Status |
|---|---|---|---|---|---|
| HR-001 | High | Baker batch sampling follows `frameRate`; realtime rate is not guaranteed | Baker runtime | exact batch timestamps and one realtime sample per rendered frame | Verified |
| HR-002 | High | Batch playback and sampling share one clock | Baker runtime | timeScale 0/0.5/1/2 tests | Verified |
| HR-003 | High | Invalid Baker setup must not report a successful empty bake | Baker runtime/editor | validation and failure-result tests | Verified |
| HR-004 | High | Generic `ignoreList` excludes rotation only | Generic Baker | transform present in both lists keeps position | Verified |
| HR-005 | Medium | Re-baking preserves destination clip settings | Baker editor | overwrite regression | Verified |
| HR-006 | High | Generic Baker supports Legacy input/output | Baker runtime/editor | Legacy batch regression | Verified |
| HR-007 | High | Mapping, pin and muscle master weights are independent | animation matching | 0/1 authority matrix | Verified |
| HR-008 | High | COM module exposes pressure, COM vector and angle | behaviours | multi-contact and arbitrary-gravity tests | Verified |
| HR-009 | High | BehaviourPuppet respawn resets pose and state atomically | BehaviourPuppet | respawn from every state | Verified from Unpinned, GetUp, Dead and Frozen with immediate active lifecycle restoration |
| HR-010 | Medium | Quadruped get-up uses right/left side classification | BehaviourPuppet | orientation and event tests | Verified in initialized runtime with prone/supine event routing |
| HR-011 | Medium | BehaviourPuppet exposes any-collision and collider-surface operations | BehaviourPuppet | filtered/unfiltered collision tests | Verified, including exact material/enabled-state lifecycle rollback |
| HR-012 | Medium | BehaviourFall documented fields are runtime configurable | BehaviourFall | setter/re-cache tests | Verified with real Humanoid AnimatorController |
| HR-013 | High | PropMelee action accepts a duration | Props | expiry/re-entry/lifecycle tests | Verified including re-entry, drop and safe-boundary expiry |
| HR-014 | Medium | Held prop returns its current muscle Rigidbody | Props | held/dropped/transition tests | Verified |
| HR-015 | Medium | Additional pin can be added/removed while held | Props | live mutation and rollback tests | Verified |
| HR-016 | High | Automatic setup creates the complete Target/Puppet rig | authoring/setup | editor and runtime integration tests | Verified: three runtime variants plus Editor commit, Undo/Redo and rollback |
| HR-017 | Medium | Automatic mass distribution is biometric | authoring | normalized regional-mass tests | Verified |
| HR-018 | High | Manual physics and Legacy Animation lifecycles are supported | animator lifecycle | scripted-simulation and Legacy tests | Verified: scripted step plus Legacy kill, freeze, resurrect and respawn restoration |
| HR-019 | Medium | Post-init and documented dynamic hierarchy operations are public | animator lifecycle/hierarchy | ordering and rollback tests | Verified: collection/replacement, stale handles, event order and rollback |
| HR-020 | High | Historical coverage documents contain no unsupported closure | documentation | full 140-row re-audit | Reopened by R11/R12/R34: historical 139 V and G05 N/A are not current certification |
| HR-021 | High | Automatic ragdoll creator remains live-editable and ownership-safe | authoring | validate-before-rebuild, foreign-component and Undo/Redo tests | Verified |
