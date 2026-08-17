# R01-R34 repair register

Status date: 2026-08-11. This is the historical register that reopened 0050 for the
**Hairibar.Ragdoll package only**. The executable schema 3 manifest has now closed
that reopening with 139 `Verified`, G05 as the only `N/A`, and zero open rows.
The per-repair statuses below describe the historical remediation pass; they are
not the current certification authority.

R24-R33 were originally entered here even though they describe integration policy
owned by the CODE RED consumer (AI, NavMesh, pooling, visuals, hit routing and
weapons). They are preserved verbatim in the
[consumer integration history](../Integration/CODE-RED-CONSUMER-HISTORY.md), but are
not package requirements, package evidence or gates for the 140-row package matrix.
Closing Hairibar.Ragdoll neither asserts nor requires completion of those consumer
items.

## Latest executed evidence (2026-08-08)

Package implementation and regression work for R01-R23 is present, but this register
deliberately remains open until R34 links every historical package capability to
unique executable evidence. The latest clean-clone package execution established:

- EditMode: `219/219`; SHA-256
  `ACEEC802F7D23A40A5FD66BA5EF311549B367CD2ECB8783298CA954336951565`.
- PlayMode: `558/558`; SHA-256
  `5D24717961CA19AF5DCAB433C1533BE4893F6340243965E13085694031F4E890`.
- Combined NUnit evidence: `777/777`, zero skipped/inconclusive; SHA-256
  `3E709F8D8DE50E172AED8663CE03903C816EB8426A080890615C92FA5099F262`.
- Development `BuildReport`: Windows64, Linux64, macOS and WebGL succeeded with
  zero diagnostics owned by `com.hairibar.ragdoll`; SHA-256
  `A87DCD314ACB9C34000AB1540E11B362552E216D3C400F4DB0DAF49DF6D18E8D`.
- Windows Development Player: four scenarios, 117 assertions and 16 performance
  cells succeeded; SHA-256
  `1B716321E2676B29332E5BA09BD63553FA7FB15CCC432D231720688814C56EAC`.
- Critical-path GC: Baker Realtime `0 B`; additional pin `0 B`; the 1/10/25/50
  Active-tree, Active-flat, Kinematic and Disabled matrix recorded `0 B` after
  warm-up. A `64 B` whole-frame ambient sample in HierarchyProps is retained as
  diagnostic data and is not attributed to the measured Hairibar call path.
- Executable 140-row manifest: `41 Verified`, `98 Open`, `1 N/A (G05)`; SHA-256
  `169D6083D2B4DAD14001402200DBC965971E21D3C68B6772E20EE329E5BDC8C7`.

Linux was built on the Windows host but not executed. The performance figures are
observations for this run, not universal RootMotion or Hairibar thresholds.

| ID | Official contract | Area | Required executable proof | Status |
|---|---|---|---|---|
| R01 | BehaviourPuppet `state [get,set]` | Puppet behaviour | Explicit Puppet/Unpinned/GetUp transitions and events | In progress |
| R02 | PuppetMaster `SetMuscleWeights` overloads | Muscle authority | Index/Transform/Humanoid/group/recursive matrix | In progress |
| R03 | PuppetMaster mode and state properties | Simulation modes | Runtime set/read during every transition | In progress |
| R04 | PuppetMaster `targetAnimator` | BehaviourFall | External assigned Animator integration | In progress |
| R05 | PuppetMaster muscle damper | Joint drives | Absolute damper plus serialized multiplier migration | In progress |
| R06 | PuppetMaster `ReplaceMuscle` | Dynamic hierarchy | Root replacement with descendants, props and rollback | In progress |
| R07 | PuppetMaster flatten/tree hierarchy | Dynamic hierarchy | Root and every muscle layout/pose preservation | In progress |
| R08 | BehaviourPuppet collision delegates | Collision events | Any collision versus pin-loss event phases | In progress |
| R09 | PuppetMaster manual simulation | Manual simulation | Invalid-order cleanup restores Animator and flags | In progress |
| R10 | Hairibar event isolation design | Behaviour events | Failing middle listener does not suppress later listeners | In progress |
| R11 | Setup/IK/PuppetMaster pages | Closure tests | Five IDs execute five distinct focused scenarios | In progress |
| R12 | Unity Test Framework/BuildPipeline/Profiler | Certification | No source-string or circular status tests | In progress |
| R13 | BehaviourFall/IK/Baker | Humanoid regression | Real Fall, scheduler, solver and recorded clip | In progress |
| R14 | PuppetMaster melee prop | Props | Held Capsule replaces Box before/during/after action | In progress |
| R15 | PuppetMaster melee action pin | Props | Absolute action pin with non-unit base pin | In progress |
| R16 | PuppetMaster live ragdoll creation | Live authoring | Inspector/script/prefab/Undo dirty detection | In progress |
| R17 | Hairibar transactional authoring design | Live authoring | Injected construction failure preserves old rig | In progress |
| R18 | PuppetMaster symmetric editing | Authored editor | Mirrored limits and single Undo transaction | In progress |
| R19 | PuppetMaster symmetry threshold | Authored editor | Remote candidate rejected | In progress |
| R20 | Baker Generic/Legacy looping | Baker editor | First/last keys equal without settings loss | In progress |
| R21 | Hairibar cancellation contract | Baker runtime/editor | Cancel leaves destination asset unchanged | In progress |
| R22 | Unity PackageManager Sample import | Certification assets | Second preparation performs no import/recompile | In progress |
| R23 | Unity BuildReport | Certification builds | Hairibar warnings fail; external warnings are reported | In progress |
| R34 | RootMotion public reference and Unity tests | 140-row matrix | Source, exact test and artifact for every row | In progress |

The identifier gap R24-R33 is intentional. IDs are not renumbered because they are
stable historical references; their relocation cannot change the meaning of R01-R23
or R34.

Normative indexes:

- https://root-motion.com/puppetmasterdox/html/classes.html
- https://root-motion.com/puppetmasterdox/html/pages.html
- https://docs.unity3d.com/
