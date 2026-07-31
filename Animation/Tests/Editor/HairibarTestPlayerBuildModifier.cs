using UnityEditor;
using UnityEditor.TestTools;

[assembly: TestPlayerBuildModifier(
    typeof(Hairibar.Ragdoll.Animation.Editor.Tests.HairibarTestPlayerBuildModifier))]

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    /// <summary>Applies Unity's documented Development Player test flags.</summary>
    public sealed class HairibarTestPlayerBuildModifier : ITestPlayerBuildModifier
    {
        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions playerOptions)
        {
            playerOptions.options |= BuildOptions.Development;
            playerOptions.options |= BuildOptions.AllowDebugging;
            return playerOptions;
        }
    }
}
