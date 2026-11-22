using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenSekai.App.Library
{
    /// <summary>System document picker. Cancellation yields false/null; I/O and format failures fault the task.</summary>
    public static class ProfileBackupPicker
    {
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
                if (await AndroidDocumentPicker.SaveAsync(file) == null) return false;
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
            string path = await AndroidDocumentPicker.OpenPlayerBackupAsync();
            if (path == null) return null;
            try { return await store.PreviewBackupAsync(path, cancel); }
            finally { File.Delete(path); }
#else
            throw new PlatformNotSupportedException();
#endif
        }
    }
}
