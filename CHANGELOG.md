# Changelog

## 2.0.0 - 2026-07-22

- Added complete modular behaviour lifecycle, Puppet/Fall behaviours, serializable events and reusable sub-behaviours.
- Added Alive/Dead/Frozen lifecycle, simulation modes, teleport, dynamic muscles, disconnect/reconnect, joint-break handling and advanced pin/joint/collision policies.
- Added transactional props, additional pinning and melee actions.
- Added pre/post-physics IK scheduling through a package-independent solver adapter.
- Added Humanoid/manual-Generic biped authoring, runtime creation, Scene handles, symmetry, collider/joint tools, flat/tree layouts, layer validation and portable Humanoid bindings.
- Added Generic/Legacy and Humanoid Baker workflows for clips, states, PlayableDirector and realtime physics, including Root/Foot/Hand IK and independent key reduction.
- Raised the minimum supported Unity version to Unity 6 (`6000.0`).
- Removed all third-party package dependencies and replaced the small compatibility surface with original package-local utilities.
- Validated with Unity 6000.5.2f1 EditMode and PlayMode test suites; this is not
  a PuppetMaster parity certificate while the versioned coverage audit remains open.
- Fixed world-space ConfigurableJoint target rotation, behaviour-event coverage,
  quality/lifecycle reconciliation, behaviour-switch rollback, collision-hub late
  registration, saturated ground raycasts and non-finite physics settings found by
  the post-sprint hostile audit.

# [v1.4.1](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.4.0...v1.4.1)

> 19 February 2021




### Fixes

- Dealt with some weird ConfigurableJoint behaviour when reenabling a ragdoll [`a57e6d1`](https://github.com/hairibar/Hairibar.Ragdoll/commit/a57e6d16c915722625514ff06f68b5e6613b731e)



# [v1.4.0](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.3.3...v1.4.0)

> 19 February 2021



### New Features

- Made RagdollAnimator.SnapToTargetPose() public [`ade603d`](https://github.com/hairibar/Hairibar.Ragdoll/commit/ade603de657b173204cde7fde597a01c317b9c4a)




# [v1.3.3](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.3.2...v1.3.3)

> 19 February 2021




### Fixes

- Fixed possible NullReferenceException in RagdollCollisionEventDispatcher [`14a3ff8`](https://github.com/hairibar/Hairibar.Ragdoll/commit/14a3ff890866c69c12da5da6ae646da08c540a90)

### Documentation

- Updated README install instructions [`89fcbe0`](https://github.com/hairibar/Hairibar.Ragdoll/commit/89fcbe0e908c458fac6263de6141bb05deb8d7cf)


# [v1.3.2](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.3.1...v1.3.2)

> 18 February 2021




### Fixes

- Fixed possible NullReferenceException when destroying the ragdoll [`72f80ff`](https://github.com/hairibar/Hairibar.Ragdoll/commit/72f80ff277a125105e7751ed49015426099139cc)



# [v1.3.1](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.3.0...v1.3.1)

> 18 February 2021




### Fixes

- Take render pipeline into account when creating materials for RagdollColliderVisualizer [`81c55c0`](https://github.com/hairibar/Hairibar.Ragdoll/commit/81c55c0fbcf905676225c689b42e55cd2165ab87)



# [v1.3.0](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.2.1...v1.3.0)

> 18 February 2021







# [v1.2.1](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.2.0...v1.2.1)

> 12 November 2020







# [v1.2.0](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.1.0...v1.2.0)

> 11 November 2020







# [v1.1.0](https://github.com/hairibar/Hairibar.Ragdoll/compare/v1.0.0...v1.1.0)

> 5 October 2020







# v1.0.0

> 10 April 2020







