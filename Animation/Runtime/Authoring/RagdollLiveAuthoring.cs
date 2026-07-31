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
            if (authoredRig) RagdollRuntimeAuthoring.Clear(authoredRig);
            authoredRig = null;
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

        internal void SetAuthoredRig(RagdollAuthoredRig value)
        {
            authoredRig = value;
        }

        void OnValidate()
        {
            options.Normalize();
        }
    }
}
