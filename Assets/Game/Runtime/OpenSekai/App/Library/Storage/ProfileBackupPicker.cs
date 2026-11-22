using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenSekai.App.Library
{
    /// <summary>System document picker. Cancellation yields false/null; I/O and format failures fault the task.</summary>
    public sealed class ProfileBackupPicker : MonoBehaviour
    {
        private static TaskCompletionSource<string> _pending;

        public static async Task<bool> ExportAsync(CancellationToken cancel = default, PlayerProfileStore store = null)
        {
            store ??= PlayerProfileStore.Local;
#if UNITY_EDITOR
            string destination = UnityEditor.EditorUtility.SaveFilePanel("导出个人档案", "", "OpenSekai-Player", "zip");
            if (string.IsNullOrEmpty(destination)) return false;
            await store.ExportBackupAsync(destination, cancel);
            return true;
#elif UNITY_ANDROID
            string directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string file = Path.Combine(directory, "OpenSekai-Player.zip");
                var copy = await store.WriteBackupCopyAsync(file, cancel);
                if (await Pick(file) == null) return false;
                cancel.ThrowIfCancellationRequested();
                store.MarkBackupSaved(copy.Directory, copy.Bytes);
                return true;
            }
            finally { Directory.Delete(directory, true); }
#else
            throw new PlatformNotSupportedException();
#endif
        }

        public static async Task<ProfileImportPreview> PickImportAsync(CancellationToken cancel = default, PlayerProfileStore store = null)
        {
            store ??= PlayerProfileStore.Local;
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("导入个人档案", "", "zip");
            return string.IsNullOrEmpty(path) ? null : await store.PreviewBackupAsync(path, cancel);
#elif UNITY_ANDROID
            string path = await Pick(null);
            if (path == null) return null;
            try { return await store.PreviewBackupAsync(path, cancel); }
            finally { File.Delete(path); }
#else
            throw new PlatformNotSupportedException();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<string> Pick(string source)
        {
            if (_pending != null) throw new InvalidOperationException("File picker is busy.");
            var receiver = new GameObject("OpenSekaiProfilePicker-" + Guid.NewGuid().ToString("N"));
            DontDestroyOnLoad(receiver);
            receiver.AddComponent<ProfileBackupPicker>();
            _pending = new TaskCompletionSource<string>();
            try
            {
                using var java = new AndroidJavaClass("com.opensekai.ShareExportHelper");
                if (source == null) java.CallStatic("OpenPlayerBackup", receiver.name, nameof(OnDocument));
                else java.CallStatic("SaveAndShare", source, receiver.name, nameof(OnDocument));
                return await _pending.Task;
            }
            finally
            {
                _pending = null;
                Destroy(receiver);
            }
        }
#endif

        [Preserve]
        public void OnDocument(string message)
        {
            if (message == "cancelled") _pending?.TrySetResult(null);
            else if (message == "success") _pending?.TrySetResult(string.Empty);
            else if (message.StartsWith("success:", StringComparison.Ordinal) && message.Length > 8)
                _pending?.TrySetResult(message.Substring(8));
            else _pending?.TrySetException(new IOException(message));
        }
    }
}
