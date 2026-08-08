# R01-R34 repair register

Status date: 2026-08-08. This register reopens the previous 0050 closure. A row
may become `Verified` only when its official contract, focused regression and
executed artifact are recorded. `In progress` is not certification.

## Latest executed evidence (2026-08-08)

The implementation and regression work for R01-R33 is present, but this register
deliberately remains open until R34 links every historical capability to unique
executable evidence. The latest clean-clone execution established:

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
| R24 | PuppetMaster Active/Unpinned | CODE RED authority | Critical hit retains BehaviourPuppet muscle policy | In progress |
| R25 | BehaviourPuppet `canMoveTarget` | CODE RED recovery | Pelvis/Target/actor/NavMesh continuity | In progress |
| R26 | PuppetMaster Resurrect | CODE RED lifecycle | Alive recovery never requests resurrection | In progress |
| R27 | PuppetMaster `targetAnimator` | CODE RED animation | Visual_UAL1 is sole bound Animator | In progress |
| R28 | Unity Rigidbody velocity | CODE RED death | Inherited velocity reaches every live muscle | In progress |
| R29 | BehaviourPuppet Reset | CODE RED reuse | Kill/reset/move/reuse/second-kill scenario | In progress |
| R30 | PuppetMaster Teleport/BehaviourPuppet Reset | CODE RED teleport | All lifecycle and physics states | In progress |
| R31 | BehaviourPuppet Unpinned policy | CODE RED impacts | All impact order permutations | In progress |
| R32 | Unity collider/Rigidbody identity | CODE RED hit lookup | Foreign homonymous body rejected | In progress |
| R33 | PuppetMaster props | CODE RED weapons | Pickup/drop/melee/owner/death/reuse/branch replacement | In progress |
| R34 | RootMotion public reference and Unity tests | 140-row matrix | Source, exact test and artifact for every row | In progress |

Normative indexes:

- http://www.root-motion.com/puppetmasterdox/html/classes.html
- http://www.root-motion.com/puppetmasterdox/html/pages.html
- https://docs.unity3d.com/
