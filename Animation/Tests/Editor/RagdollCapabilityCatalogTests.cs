using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollCapabilityCatalogTests
    {
        static readonly Regex ValidId = new Regex("^[A-J][0-9]{2}$");

        [Test]
        public void CatalogContainsExactClosedWorldInventoryWithTypedAndEvidence()
        {
            var contracts = RagdollCapabilityCatalog.Contracts;

            Assert.That(contracts, Has.Count.EqualTo(RagdollCapabilityCatalog.ExpectedCount));
            Assert.That(contracts.Select(contract => contract.Id).Distinct().Count(),
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount));
            Assert.That(contracts.All(contract => ValidId.IsMatch(contract.Id)), Is.True);
            Assert.That(contracts.Count(contract => contract.IsApplicable),
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount - 1));

            string[] expected =
                Range("A", 12).Concat(Range("B", 30))
                .Concat(Range("C", 7)).Concat(Range("D", 46))
                .Concat(Range("E", 3)).Concat(Range("F", 14))
                .Concat(Range("G", 5)).Concat(Range("H", 8))
                .Concat(Range("I", 10)).Concat(Range("J", 7))
                .ToArray();
            Assert.That(contracts.Select(contract => contract.Id), Is.EqualTo(expected));

            foreach (RagdollCapabilityContract contract in contracts)
            {
                Assert.That(contract.OfficialSource, Is.Not.Empty, contract.Id);
                Assert.That(contract.SourceLocator, Is.Not.Empty, contract.Id);
                Assert.That(contract.ObservableClaim, Is.Not.Empty, contract.Id);
                Assert.That(contract.AffectedApis, Is.Not.Empty, contract.Id);
                Assert.That(contract.OfficialSource.Split(';').Select(source => source.Trim()),
                    Is.All.Matches<string>(source =>
                        source.StartsWith("http://www.root-motion.com/puppetmasterdox/html/",
                            StringComparison.Ordinal)
                        || source.StartsWith("https://docs.unity3d.com/6000.0/Documentation/",
                            StringComparison.Ordinal)), contract.Id);
                Assert.That(contract.RequiredEvidence.Distinct().Count(),
                    Is.EqualTo(contract.RequiredEvidence.Count), contract.Id);
            }

            RagdollCapabilityContract excluded = RagdollCapabilityCatalog.Get("G05");
            Assert.That(excluded.IsApplicable, Is.False);
            Assert.That(excluded.ExclusionReason, Does.Contain("Final IK"));
            Assert.That(excluded.RequiredEvidence, Is.Empty);
            Assert.That(contracts.Where(contract => contract.Id != "G05"),
                Is.All.Matches<RagdollCapabilityContract>(contract =>
                    contract.IsApplicable && contract.RequiredEvidence.Count != 0));
        }

        [Test]
        public void MultiArtifactRequirementsRetainAndSemantics()
        {
            Assert.That(RagdollCapabilityCatalog.Get("H05").RequiredEvidence,
                Is.EquivalentTo(new[]
                {
                    RagdollEvidenceKind.NUnitPlayMode,
                    RagdollEvidenceKind.WindowsPlayerScenario,
                    RagdollEvidenceKind.ProfilerResult,
                }));
            Assert.That(RagdollCapabilityCatalog.Get("J04").RequiredEvidence,
                Is.EquivalentTo(new[]
                {
                    RagdollEvidenceKind.SceneArtifact,
                    RagdollEvidenceKind.WindowsPlayerScenario,
                }));
            Assert.That(RagdollCapabilityCatalog.Get("J05").RequiredEvidence,
                Is.EquivalentTo(new[]
                {
                    RagdollEvidenceKind.ProfilerResult,
                    RagdollEvidenceKind.WindowsPlayerScenario,
                }));

            RagdollCapabilityContract realtime =
                RagdollCapabilityCatalog.Get("I07");
            Assert.That(
                realtime.ExactNUnitEvidenceTests[
                    RagdollEvidenceKind.NUnitPlayMode],
                Is.EqualTo(
                    "Hairibar.Ragdoll.Animation.Tests." +
                    "RagdollBakerRealtimeFramePlayModeEvidence." +
                    "RealtimeSamplesAtMostOncePerRenderedFrame"));
        }

        static string[] Range(string prefix, int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => prefix + index.ToString("00"))
                .ToArray();
        }
    }
}
