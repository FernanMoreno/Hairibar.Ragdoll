using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Deterministic registry and active-selection state for a behaviour controller.
    /// The collection owns no Unity lifecycle; the controller performs callbacks and
    /// component enable/disable changes after a selection has been accepted.
    /// </summary>
    internal sealed class RagdollBehaviourCollection
    {
        readonly RagdollBehaviourBase[] behaviours;
        readonly IReadOnlyList<RagdollBehaviourBase> behavioursView;

        internal IReadOnlyList<RagdollBehaviourBase> Behaviours => behavioursView;
        internal RagdollBehaviourBase Active { get; private set; }

        internal RagdollBehaviourCollection(RagdollBehaviourBase[] source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            behaviours = new RagdollBehaviourBase[source.Length];
            Array.Copy(source, behaviours, source.Length);
            behavioursView = behaviours;

            for (int index = 0; index < behaviours.Length; index++)
            {
                RagdollBehaviourBase behaviour = behaviours[index];
                if (!behaviour)
                {
                    throw new ArgumentException(
                        "A behaviour collection cannot contain null components.",
                        nameof(source));
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (ReferenceEquals(behaviours[previous], behaviour))
                    {
                        throw new ArgumentException(
                            "A behaviour collection cannot contain the same component twice.",
                            nameof(source));
                    }
                }
            }
        }

        internal bool Contains(RagdollBehaviourBase behaviour)
        {
            if (!behaviour) return false;

            for (int index = 0; index < behaviours.Length; index++)
            {
                if (ReferenceEquals(behaviours[index], behaviour))
                {
                    return true;
                }
            }

            return false;
        }

        internal RagdollBehaviourBase FindInitiallyEnabled(out int enabledCount)
        {
            RagdollBehaviourBase first = null;
            enabledCount = 0;

            for (int index = 0; index < behaviours.Length; index++)
            {
                RagdollBehaviourBase behaviour = behaviours[index];
                if (!behaviour.enabled || !behaviour.gameObject.activeSelf) continue;

                enabledCount++;
                if (!first)
                {
                    first = behaviour;
                }
            }

            return first;
        }

        internal bool TrySetActive(
            RagdollBehaviourBase next,
            out RagdollBehaviourBase previous)
        {
            if (next && !Contains(next))
            {
                throw new ArgumentException(
                    "The requested behaviour does not belong to this controller.",
                    nameof(next));
            }

            previous = Active;
            if (ReferenceEquals(previous, next))
            {
                return false;
            }

            Active = next;
            return true;
        }
    }
}
