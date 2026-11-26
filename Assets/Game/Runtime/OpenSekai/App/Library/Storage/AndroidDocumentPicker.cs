using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenSekai.App.Library
{
    /// <summary>Receives one Android document result. Callers own the cache copy returned by an open request.</summary>
    internal sealed class AndroidDocumentPicker : MonoBehaviour
    {
        private static AndroidDocumentPicker _active;
        private readonly TaskCompletionSource<string> _result = new TaskCompletionSource<string>();

        internal static Task<string> OpenSongPackageAsync() =>
            PickAsync("OpenSongPackage", receiver => new object[] { receiver, nameof(OnDocument) });
        internal static Task<string> OpenPlayerBackupAsync() => PickAsync("OpenPlayerBackup", PlayerBackupArguments);
        internal static Task<string> SaveAsync(string source) =>
            PickAsync("SaveAndShare", receiver => new object[] { source, receiver, nameof(OnDocument) });

        internal static object[] PlayerBackupArguments(string receiver) =>
            new object[] { receiver, nameof(OnDocument), ProfileBackupArchive.MAX_ARCHIVE_BYTES };

        private static async Task<string> PickAsync(string method, Func<string, object[]> arguments)
        {
            if (_active != null) throw new InvalidOperationException("已有文件选择操作正在进行。");
            var receiver = new GameObject("OpenSekaiDocumentPicker-" + Guid.NewGuid().ToString("N"));
            DontDestroyOnLoad(receiver);
            _active = receiver.AddComponent<AndroidDocumentPicker>();
            try
            {
                using var java = new AndroidJavaClass("com.opensekai.ShareExportHelper");
                java.CallStatic(method, arguments(receiver.name));
                return await _active._result.Task;
            }
            finally
            {
                _active = null;
                Destroy(receiver);
            }
        }

        [Preserve]
        public void OnDocument(string message)
        {
            if (message == "cancelled") _result.TrySetResult(null);
            else if (message == "success") _result.TrySetResult(string.Empty);
            else if (message.StartsWith("success:", StringComparison.Ordinal) && message.Length > 8)
                _result.TrySetResult(message.Substring(8));
            else _result.TrySetException(new IOException(message.StartsWith("error:", StringComparison.Ordinal)
                ? message.Substring(6) : message));
        }
    }
}
