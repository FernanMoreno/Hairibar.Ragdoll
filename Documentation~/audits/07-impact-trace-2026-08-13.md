# Impact trace: Unpinned -> Puppet

## Scope

CODE RED production-grunt strong impact, observed for three physics ticks.

## Result

`ImpactTrace_UnpinDoesNotSwitchBehaviourOrResetPuppet` passes.

The trace proves:

1. `RagdollPuppetBehaviour` transitions `Puppet -> Unpinned`.
2. `RagdollBehaviourController.ActiveBehaviourChanged` receives no event.
3. The active behaviour remains `RagdollPuppetBehaviour` on ticks 0, 1 and 2.
4. Puppet state remains `Unpinned` on those ticks.

Therefore a behaviour switch is not the cause of the observed production
strong-impact transition in this configured flow. The structural reset remains
relevant if another caller activates a behaviour after an Unpin, but it is not
present here.

## API correction

CODE RED now invokes `RagdollPuppetBehaviour.Unpin()` and derives success from
the resulting state. It no longer uses direct `SetState(Unpinned)` for impacts.

## Verification

`HairibarRagdollAdapterPlayModeTests`: 28 / 28 passed in Unity headless.
