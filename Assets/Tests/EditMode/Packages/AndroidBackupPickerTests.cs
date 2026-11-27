using NUnit.Framework;
using OpenSekai.App.Library;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class AndroidBackupPickerTests
    {
        [Test]
        public void PickerPassesTheCompleteV4ArchiveBudgetToAndroid()
        {
            const long MEBIBYTE = 1024L * 1024;
            // A v4 backup can hold 64 MiB each of history, challenges, achievements and purchases, plus ZIP overhead.
            var arguments = AndroidDocumentPicker.PlayerBackupArguments("backup-test");
            Assert.That(arguments, Is.EqualTo(new object[] { "backup-test", "OnDocument", 257 * MEBIBYTE }));
        }
    }
}
