using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollPuppetBehaviour))]
    internal sealed class RagdollPuppetBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RagdollPuppetBehaviour behaviour =
                (RagdollPuppetBehaviour) target;

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Configure Ground Layers so they include walkable geometry. "
                    + "Collision Layers filters contacts, Collision Threshold is squared impulse, and Max Collisions is the accepted-event budget. "
                    + "Collision Resistance can be constant or evaluated from sampled Target speed; layer rules use first-match order. "
                    + "Regain Pin Speed composes with the muscle-controller base rate and semantic group multipliers; Muscle Weight Relative To Pin affects rotational authority only in Puppet state. "
                    + "Combat boosts temporarily raise incoming immunity or outgoing impulse damage and fall back to neutral at Boost Falloff; use the runtime API globally, by bone, with parent/child falloff, or by semantic group. "
                    + "Max Rigidbody Velocity clamps physical and sampled Target velocity on the transition to Unpinned; Unpinned Muscle Knockout controls whether zero-configured-pin muscles may knock out the whole Puppet. "
                    + "Blend To Animation Time controls Target blending independently from Minimum Get Up Duration; Get Up Collision Resistance and Get Up Regain Pin Speed multiply their base values only while GetUp is active. "
                    + "Can Move Target is a runtime ownership switch: when false the behaviour consumes GetUp alignment without moving the Target root. NotifyTeleported must be called only after an external teleport has completed. "
                    + "Muscle-profile surface settings can disable colliders only in Puppet and assign shared PhysicMaterials for Puppet/GetUp or Unpinned; the captured baseline is restored when the behaviour deactivates. "
                    + "Normal Mode Unmapped suppresses mapping without contact. Kinematic delegates global Rigidbody mode changes to RagdollSimulationModeController and activates only from accepted contacts that satisfy its source and impulse filters. "
                    + "Body Front Axis must point out of the chest and Body Up Axis must match character up while standing.",
                    MessageType.Info);

                if (behaviour.NormalMode == RagdollPuppetNormalMode.Kinematic)
                {
                    RagdollBehaviourController controller =
                        behaviour.GetComponentInParent<RagdollBehaviourController>();
                    RagdollSimulationModeController simulation =
                        controller
                            ? controller.GetComponent<RagdollSimulationModeController>()
                            : null;
                    if (!simulation)
                    {
                        EditorGUILayout.HelpBox(
                            "RagdollAnimator will create a default RagdollSimulationModeController at runtime. Add the component explicitly if you need to author its transition duration, initial mode or lifetime policy.",
                            MessageType.Info);
                    }
                    else if (!simulation.enabled)
                    {
                        EditorGUILayout.HelpBox(
                            "RagdollSimulationModeController is disabled. Enable it before entering Play Mode.",
                            MessageType.Error);
                    }
                }

                return;
            }

            RagdollGroundingSnapshot grounding = behaviour.Grounding;
            RagdollPuppetCollisionStepSnapshot collisionStep = behaviour.CollisionStep;
            RagdollPuppetCollisionResponseSnapshot collisionResponse =
                behaviour.LastCollisionResponse;
            EditorGUILayout.HelpBox(
                "State: " + behaviour.State
                + "\nNormal mode: " + behaviour.NormalMode
                + "\nNormal mapping weight: "
                + behaviour.NormalModeMappingWeight.ToString("P0")
                + "\nUnmapped contact active: "
                + behaviour.UnmappedContactActive
                + "\nRegain pin configured / applied: "
                + behaviour.RegainPinSpeed.ToString("F2") + " / "
                + behaviour.AppliedRegainPinSpeed.ToString("F2")
                + "\nMuscle relative to pin: "
                + behaviour.MuscleWeightRelativeToPinWeight.ToString("P0")
                + "\nBoost falloff / active: "
                + behaviour.BoostFalloff.ToString("F2") + " / "
                + behaviour.HasActiveBoosts
                + "\nMaximum immunity / impulse multiplier: "
                + behaviour.MaximumImmunity.ToString("P0") + " / "
                + behaviour.MaximumImpulseMultiplier.ToString("F2")
                + "\nMax Rigidbody velocity: "
                + behaviour.MaxRigidbodyVelocity.ToString("F2")
                + "\nZero-configured-pin knockout: "
                + behaviour.UnpinnedMuscleKnockout
                + "\nAutomatic GetUp: "
                + behaviour.CanGetUp
                + "\nGetUp delay / max speed: "
                + behaviour.GetUpDelay.ToString("F2") + " / "
                + behaviour.MaxGetUpVelocity.ToString("F2")
                + "\nGetUp blend / minimum duration: "
                + behaviour.BlendToAnimationTime.ToString("F2") + " / "
                + behaviour.MinGetUpDuration.ToString("F2")
                + "\nGetUp collision / regain / knockout multipliers: "
                + behaviour.GetUpCollisionResistanceMlp.ToString("F2") + " / "
                + behaviour.GetUpRegainPinSpeedMlp.ToString("F2") + " / "
                + behaviour.GetUpKnockOutDistanceMlp.ToString("F2")
                + "\nCan move Target / alignment pending / teleport-completed blend: "
                + behaviour.CanMoveTarget + " / "
                + behaviour.TargetAlignmentPending + " / "
                + behaviour.GetUpBlendCompletedByTeleport
                + "\nSurface state / baseline: "
                + behaviour.SurfaceState + " / "
                + behaviour.SurfaceBaselineCaptured
                + "\nSurface colliders / disabled / material overrides: "
                + behaviour.SurfaceColliderCount + " / "
                + behaviour.SurfaceDisabledColliderCount + " / "
                + behaviour.SurfaceMaterialOverrideCount
                + "\nState time: " + behaviour.StateElapsedTime.ToString("F2")
                + "\nGetUp progress: " + behaviour.GetUpProgress.ToString("P0")
                + "\nOrientation: " + behaviour.GetUpOrientation
                + "\nGrounded: " + grounding.IsGrounded
                + "\nStable ground time: " + grounding.StableTime.ToString("F2")
                + "\nCOM speed: " + grounding.CenterOfMassVelocity.magnitude.ToString("F2")
                + "\nReady to get up: " + behaviour.CanBeginGetUp
                + "\nAccepted collisions: " + behaviour.AcceptedCollisionCount
                + "\nRejected collisions: " + behaviour.RejectedCollisionCount
                + "\nLast rejection: " + behaviour.LastCollisionRejectionReason
                + "\nCurrent physics step: "
                + (collisionStep.HasStep ? collisionStep.FixedTime.ToString("F3") : "none")
                + "\nStep reported / accepted / rejected: "
                + collisionStep.ReportedCount + " / "
                + collisionStep.AcceptedCount + " / "
                + collisionStep.RejectedCount
                + "\nLast raw / damage impulse and source multiplier: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.ImpulseMagnitude.ToString("F2") + " / "
                        + collisionResponse.DamageImpulseMagnitude.ToString("F2") + " / "
                        + collisionResponse.SourceImpulseMultiplier.ToString("F2")
                    : "none")
                + "\nLast immunity / unmitigated / applied suppression: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.ReceivingImmunity.ToString("P0") + " / "
                        + collisionResponse.UnmitigatedPositionSuppression.ToString("P0") + " / "
                        + collisionResponse.PositionSuppression.ToString("P0")
                    : "none")
                + "\nTarget speed / state resistance / effective resistance: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.TargetSpeed.ToString("F2") + " / "
                        + collisionResponse.StateResistanceMultiplier.ToString("F2") + " / "
                        + collisionResponse.EffectiveResistance.ToString("F2")
                    : "none")
                + "\nMatched layer rule: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.LayerRuleIndex.ToString()
                    : "none")
                + "\nSimulation mode / target: "
                + (behaviour.SimulationModeController
                    ? behaviour.SimulationModeController.CurrentMode + " / "
                        + behaviour.SimulationModeController.TargetMode
                    : "missing")
                + "\nKinematic managed / pending / contact: "
                + behaviour.KinematicModeManaged + " / "
                + behaviour.KinematicActivationPending + " / "
                + behaviour.KinematicActivationContactActive
                + "\nLast Kinematic activation: "
                + (behaviour.KinematicActivationCount > 0
                    ? behaviour.LastKinematicActivationSource + " @ "
                        + behaviour.LastKinematicActivationImpulse.ToString("F2")
                    : "none")
                + "\nKinematic activation count: "
                + behaviour.KinematicActivationCount,
                MessageType.Info);

            if (behaviour.IsInitialized && behaviour.SurfaceColliderCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Puppet colliders were resolved for surface-state management. Verify RagdollDefinitionBindings and each RagdollBone collider list.",
                    MessageType.Warning);
            }

            int upstreamBudget = behaviour.UpstreamMaximumEventsPerFixedStep;
            if (upstreamBudget > 0
                && upstreamBudget < behaviour.MaximumCollisionsPerFixedStep)
            {
                EditorGUILayout.HelpBox(
                    "RagdollCollisionHub is capped at " + upstreamBudget
                    + " events per physics step, below this behaviour's budget of "
                    + behaviour.MaximumCollisionsPerFixedStep
                    + ". The upstream hub can therefore discard callbacks first.",
                    MessageType.Warning);
            }

            RagdollCollisionReaction collisionReaction =
                behaviour.GetComponent<RagdollCollisionReaction>();
            if (collisionReaction
                && collisionReaction.enabled
                && collisionReaction.softenPositionMatching)
            {
                EditorGUILayout.HelpBox(
                    "RagdollCollisionReaction is also applying position suppression. Disable its position response when RagdollPuppetBehaviour owns collision unpinning, otherwise impacts will be accumulated twice.",
                    MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(!behaviour.IsInitialized || !behaviour.IsActive);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal: Active"))
            {
                behaviour.SetNormalMode(
                    RagdollPuppetNormalMode.Active,
                    true);
            }

            if (GUILayout.Button("Normal: Unmapped"))
            {
                behaviour.SetNormalMode(
                    RagdollPuppetNormalMode.Unmapped,
                    true);
            }

            EditorGUI.BeginDisabledGroup(!behaviour.KinematicSimulationAvailable);
            if (GUILayout.Button("Normal: Kinematic"))
            {
                behaviour.SetNormalMode(
                    RagdollPuppetNormalMode.Kinematic,
                    true);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Lose Balance"))
            {
                behaviour.LoseBalance();
            }

            if (GUILayout.Button("Try Begin Get Up"))
            {
                behaviour.TryBeginGetUp();
            }

            if (GUILayout.Button("Begin Prone Get Up Immediately"))
            {
                behaviour.BeginGetUpImmediately(RagdollGetUpOrientation.Prone);
            }

            if (GUILayout.Button("Begin Supine Get Up Immediately"))
            {
                behaviour.BeginGetUpImmediately(RagdollGetUpOrientation.Supine);
            }

            if (GUILayout.Button("Interrupt Get Up"))
            {
                behaviour.InterruptGetUp();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
