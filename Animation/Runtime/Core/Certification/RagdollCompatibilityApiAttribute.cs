using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Marks the deliberately supported PuppetMaster-facing or Hairibar legacy
    /// compatibility surface. Certification discovers this metadata by reflection;
    /// it must not maintain a second hand-written symbol list.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Interface | AttributeTargets.Property
        | AttributeTargets.Method | AttributeTargets.Event
        | AttributeTargets.Field,
        AllowMultiple = false, Inherited = false)]
    internal sealed class RagdollCompatibilityApiAttribute : Attribute
    {
        public string DocumentationSection { get; }
        public string OfficialSourceUrl { get; }

        public RagdollCompatibilityApiAttribute(
            string documentationSection,
            string officialSourceUrl)
        {
            DocumentationSection = documentationSection;
            OfficialSourceUrl = officialSourceUrl;
        }
    }
}
