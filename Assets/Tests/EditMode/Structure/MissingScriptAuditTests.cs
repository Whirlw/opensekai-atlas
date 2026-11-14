using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenSekai.Tests.EditMode.Structure
{
    /// <summary>
    /// P4-4 turns the missing-script baseline into a zero. The audit walks every scene and prefab and
    /// writes TestResults/missing-scripts.tsv; anything past the header is a component whose script no
    /// longer exists, i.e. a prefab or scene that lost behaviour. Running it as a test means the
    /// existing EditMode CI job covers it without extra plumbing.
    /// </summary>
    public sealed class MissingScriptAuditTests
    {
        private const string ReportPath = "TestResults/missing-scripts.tsv";

        [Test]
        public void TheProjectHasNoMissingScriptReferences()
        {
            OpenSekai.EditorTools.MissingScriptAudit.Run();

            Assert.That(File.Exists(ReportPath), Is.True, "the audit must write " + ReportPath);
            string[] lines = File.ReadAllLines(ReportPath);
            Assert.That(lines.Length, Is.GreaterThan(0), "the audit report must at least hold its header");

            string[] offenders = lines.Skip(1).Where(line => line.Trim().Length > 0).ToArray();
            Assert.That(offenders, Is.Empty,
                "components reference scripts that no longer exist: " + string.Join(" | ", offenders));
        }
    }
}
