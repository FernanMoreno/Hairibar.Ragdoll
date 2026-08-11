# CODE RED consumer integration history

> **NON-NORMATIVE — CONSUMER-ONLY — NOT PACKAGE CERTIFICATION**

This document preserves historical R24-R33 requests that were once mixed into the
Hairibar.Ragdoll repair register. They describe how one game consumes the package;
they do not specify package behavior and must never be used to open, close or verify
an entry in Hairibar.Ragdoll's 140-capability matrix.

The RootMotion and Unity references cited by those requests may guide the consumer's
adapter design. Evidence that the generic package contract works belongs in package
tests. Evidence involving CODE RED AI, health, NavMesh, pooling, visual hierarchy,
weapons or hit routing belongs in the consumer project and is not emitted or
validated by `HairibarCertification`.

## Historical items (preserved IDs)

| ID | Historical reference | Consumer-owned area | Historical requested proof | Package status |
|---|---|---|---|---|
| R24 | PuppetMaster Active/Unpinned | CODE RED authority | Critical hit retains BehaviourPuppet muscle policy | Not evaluated by package certification |
| R25 | BehaviourPuppet `canMoveTarget` | CODE RED recovery | Pelvis/Target/actor/NavMesh continuity | Not evaluated by package certification |
| R26 | PuppetMaster Resurrect | CODE RED lifecycle | Alive recovery never requests resurrection | Not evaluated by package certification |
| R27 | PuppetMaster `targetAnimator` | CODE RED animation | Visual_UAL1 is sole bound Animator | Not evaluated by package certification |
| R28 | Unity Rigidbody velocity | CODE RED death | Inherited velocity reaches every live muscle | Not evaluated by package certification |
| R29 | BehaviourPuppet Reset | CODE RED reuse | Kill/reset/move/reuse/second-kill scenario | Not evaluated by package certification |
| R30 | PuppetMaster Teleport/BehaviourPuppet Reset | CODE RED teleport | All lifecycle and physics states | Not evaluated by package certification |
| R31 | BehaviourPuppet Unpinned policy | CODE RED impacts | All impact order permutations | Not evaluated by package certification |
| R32 | Unity collider/Rigidbody identity | CODE RED hit lookup | Foreign homonymous body rejected | Not evaluated by package certification |
| R33 | PuppetMaster props | CODE RED weapons | Pickup/drop/melee/owner/death/reuse/branch replacement | Not evaluated by package certification |

No status in the final column makes a claim about the consumer project's current
implementation. It records only the scope boundary.

## Ownership boundary

Hairibar.Ragdoll owns generic, game-independent contracts such as lifecycle and
Puppet state transitions, teleport/respawn, exact muscle and collider bindings,
collision events, props, additional pinning and transactional hierarchy operations.
Those contracts are certified using package fixtures, samples and clean-project
players.

The consumer owns orchestration of damage and critical hits, AI enablement, actor and
NavMesh reconciliation, selection of a concrete visual Animator, pooling policy,
weapon ownership and conversion of gameplay impacts into package calls. Consumer
integration may depend on a released package version; package certification must not
depend on the consumer project or its assets.

## Evidence rule

- A package defect discovered through CODE RED is reproduced with a minimal,
  game-independent package test before it enters the package repair register.
- A consumer adapter defect remains in the consumer's own tracker and test suite.
- A test using CODE RED namespaces, scenes, prefabs or project settings cannot certify
  a Hairibar.Ragdoll matrix row.
- A clean Hairibar.Ragdoll certification run does not certify CODE RED integration.

Historical normative references:

- http://www.root-motion.com/puppetmasterdox/html/classes.html
- http://www.root-motion.com/puppetmasterdox/html/pages.html
- https://docs.unity3d.com/
