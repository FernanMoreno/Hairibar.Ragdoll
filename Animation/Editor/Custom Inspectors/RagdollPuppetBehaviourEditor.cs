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
                    + "Collision Layers filters BehaviourPuppet contacts, Collision Threshold is squared impulse, and Max Collisions is the accepted-event budget for one physics timestamp. "
                    + "Body Front Axis must point out of the chest and Body Up Axis must match character up while standing.",
                    MessageType.Info);
                return;
            }

            RagdollGroundingSnapshot grounding = behaviour.Grounding;
            RagdollPuppetCollisionStepSnapshot collisionStep = behaviour.CollisionStep;
            EditorGUILayout.HelpBox(
                "State: " + behaviour.State
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
                + collisionStep.RejectedCount,
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

            EditorGUI.BeginDisabledGroup(!behaviour.IsInitialized || !behaviour.IsActive);
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
