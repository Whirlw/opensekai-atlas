using NUnit.Framework;
using OpenSekai.App.Library;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class AndroidBackupPickerTests
    {
        [Test]
        public void PickerPassesTheCompleteV3ArchiveBudgetToAndroid()
        {
            const long megabyte = 1024L * 1024;
            // A v3 backup can hold 64 MiB each of history, challenges and achievements, plus ZIP overhead.
            var arguments = AndroidDocumentPicker.PlayerBackupArguments("backup-test");
            Assert.That(arguments, Is.EqualTo(new object[] { "backup-test", "OnDocument", 193 * megabyte }));
        }
    }
}
