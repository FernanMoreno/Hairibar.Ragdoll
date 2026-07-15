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
                            "NormalMode.Kinematic requires RagdollSimulationModeController on the same GameObject as RagdollAnimator and RagdollBehaviourController. Add and enable it before entering Play Mode so RagdollAnimator can initialize the modifier.",
                            MessageType.Error);
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
                + "\nLast unpin suppression: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.PositionSuppression.ToString("P0")
                    : "none")
                + "\nTarget speed / effective resistance: "
                + (collisionResponse.HasResponse
                    ? collisionResponse.TargetSpeed.ToString("F2") + " / "
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
