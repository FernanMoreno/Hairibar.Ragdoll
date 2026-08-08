using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Persistent edit-time authoring description. Rebuilds only components recorded by
    /// its RagdollAuthoredRig and never removes unrelated components.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Ragdoll/Live Ragdoll Authoring")]
    public sealed class RagdollLiveAuthoring : MonoBehaviour
    {
        [SerializeField] RagdollBipedReferences references =
            new RagdollBipedReferences();
        [SerializeField] RagdollAuthoringOptions options =
            RagdollAuthoringOptions.Default;
        [SerializeField] bool rebuildOnChange = true;
        [SerializeField, HideInInspector] RagdollAuthoredRig authoredRig;
        [SerializeField, HideInInspector] string appliedConfigurationHash;

        public RagdollBipedReferences References => references;
        public RagdollAuthoringOptions Options => options;
        public bool RebuildOnChange
        {
            get => rebuildOnChange;
            set => rebuildOnChange = value;
        }
        public RagdollAuthoredRig AuthoredRig => authoredRig;

        public bool TryValidate(out string error)
        {
            if (Application.isPlaying)
            {
                error = "Live ragdoll authoring is edit-time only.";
                return false;
            }
            if (references == null)
            {
                error = "Biped references are required.";
                return false;
            }
            if (!references.Validate(out error)) return false;
            if (references.hips != transform
                && !references.hips.IsChildOf(transform))
            {
                error = "The referenced hips must belong to this authoring hierarchy.";
                return false;
            }
            RagdollAuthoredRig existing =
                references.hips.GetComponent<RagdollAuthoredRig>();
            if (existing && existing != authoredRig)
            {
                error = "The hips already contain a ragdoll owned by another author.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Ownership-safe primitive used by editor tests and tooling validation. The
        /// public inspector path uses Undo.AddComponent for the complete Undo contract.
        /// </summary>
        internal bool TryRebuild(out string error)
        {
            if (!TryValidate(out error)) return false;
            if (!TryValidateReplacementOwnership(out error)) return false;
            if (authoredRig)
            {
                GameObject stage;
                if (!TryBuildInactiveStage(out stage, out error)) return false;
                try
                {
                    return RagdollRuntimeAuthoring.TryRebuild(
                        authoredRig,
                        references,
                        options,
                        RagdollRuntimeAuthoring.DefaultFactory,
                        out error);
                }
                finally
                {
                    if (stage) DestroyImmediate(stage);
                }
            }
            RagdollAuthoredRig rebuilt;
            if (!RagdollRuntimeAuthoring.TryBuild(
                references,
                options,
                out rebuilt,
                out error))
            {
                return false;
            }
            authoredRig = rebuilt;
            return true;
        }

        internal bool TryBuildInactiveStage(
            out GameObject stage,
            out string error)
        {
            stage = null;
            error = string.Empty;
            try
            {
                stage = Instantiate(gameObject);
                stage.name = gameObject.name + " __RagdollAuthoringStage";
                stage.hideFlags = HideFlags.HideAndDontSave;
                stage.SetActive(false);
                RagdollLiveAuthoring stagedAuthor =
                    stage.GetComponent<RagdollLiveAuthoring>();
                if (!stagedAuthor)
                {
                    error = "Inactive authoring stage did not clone its description.";
                    return false;
                }
                if (stagedAuthor.authoredRig)
                {
                    RagdollRuntimeAuthoring.Clear(stagedAuthor.authoredRig);
                    stagedAuthor.authoredRig = null;
                }
                RagdollAuthoredRig stagedRig;
                if (!RagdollRuntimeAuthoring.TryBuild(
                    stagedAuthor.references,
                    options,
                    out stagedRig,
                    out error)) return false;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Inactive authoring stage failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(error) && stage)
                {
                    DestroyImmediate(stage);
                    stage = null;
                }
            }
        }

        internal void SetAuthoredRig(RagdollAuthoredRig value)
        {
            authoredRig = value;
        }

        internal bool RebuildOnChangeEnabled => rebuildOnChange;
        internal string AppliedConfigurationHash =>
            appliedConfigurationHash ?? string.Empty;
        internal void MarkConfigurationApplied(string stableHash)
        {
            appliedConfigurationHash = stableHash ?? string.Empty;
        }

        internal bool TryValidateReplacementOwnership(out string error)
        {
            if (!authoredRig)
            {
                error = string.Empty;
                return true;
            }
            foreach (Transform bone in references.EnumerateAll())
            {
                if (!bone) continue;
                Rigidbody body = bone.GetComponent<Rigidbody>();
                if (body && Array.IndexOf(authoredRig.Rigidbodies, body) < 0)
                {
                    error = bone.name + " contains a Rigidbody not owned by this author.";
                    return false;
                }
                Collider[] colliders = bone.GetComponents<Collider>();
                for (int index = 0; index < colliders.Length; index++)
                {
                    if (Array.IndexOf(authoredRig.Colliders, colliders[index]) < 0)
                    {
                        error = bone.name + " contains a Collider not owned by this author.";
                        return false;
                    }
                }
                ConfigurableJoint[] joints = bone.GetComponents<ConfigurableJoint>();
                for (int index = 0; index < joints.Length; index++)
                {
                    if (Array.IndexOf(authoredRig.Joints, joints[index]) < 0)
                    {
                        error = bone.name + " contains a ConfigurableJoint not owned by this author.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        void OnValidate()
        {
            options.Normalize();
        }
    }
}
