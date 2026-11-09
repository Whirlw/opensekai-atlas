using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Thin platform wrapper for the two pickers the project actually ships: Android/iOS use
    /// NativeFilePicker, and the editor uses its own dialogs. There is no desktop player build,
    /// so no standalone file browser is needed.
    /// </summary>
    public static class LibraryImport
    {
        /// <summary>Folder picking only exists in the editor; the mobile picker is file-only.</summary>
        public static bool SupportsFolderPick =>
            Application.platform == RuntimePlatform.WindowsEditor
            || Application.platform == RuntimePlatform.OSXEditor
            || Application.platform == RuntimePlatform.LinuxEditor;

        /// <summary>Opens a picker for a package archive (.zip or the .oskp alias); null when cancelled.</summary>
        public static void PickZip(Action<string> onPicked)
        {
            if (onPicked == null)
            {
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            if (NativeFilePicker.IsFilePickerBusy())
            {
                onPicked(null);
                return;
            }

            NativeFilePicker.PickFile(path => onPicked(string.IsNullOrEmpty(path) ? null : path),
                "application/zip", "application/x-zip-compressed", "application/octet-stream");
#elif UNITY_EDITOR
            // Two filters so an exported .oskp (a zip under another name) stays selectable.
            string path = EditorUtility.OpenFilePanelWithFilters(
                "选择曲包（.zip / .oskp）", string.Empty, new[] { "曲包", "zip,oskp", "所有文件", "*" });
            onPicked(string.IsNullOrEmpty(path) ? null : path);
#else
            onPicked(null);
#endif
        }

        /// <summary>Opens a folder picker; only valid when <see cref="SupportsFolderPick"/> is true.</summary>
        public static void PickFolder(Action<string> onPicked)
        {
            if (onPicked == null)
            {
                return;
            }

            if (!SupportsFolderPick)
            {
                onPicked(null);
                return;
            }

#if UNITY_EDITOR
            string path = EditorUtility.OpenFolderPanel("选择曲包文件夹", string.Empty, string.Empty);
            onPicked(string.IsNullOrEmpty(path) ? null : path);
#else
            onPicked(null);
#endif
        }

        /// <summary>Opens a save dialog for exporting a package; returns null when unavailable or
        /// cancelled, in which case the caller may fall back to a default directory.</summary>
        public static string PickExportDestination(string defaultFileName)
        {
            if (!SupportsFolderPick)
            {
                return null;
            }

#if UNITY_EDITOR
            string path = EditorUtility.SaveFilePanel("导出曲包", string.Empty, defaultFileName, "oskp");
            return string.IsNullOrEmpty(path) ? null : path;
#else
            return null;
#endif
        }

        public static string DescribeFailure(Exception exception)
        {
            if (exception == null)
            {
                return "导入失败。";
            }

            Debug.LogWarning("[LibraryImport] " + exception);

            if (exception is InvalidDataException)
            {
                return "不是有效的曲包文件：请选择 .zip 或 .oskp（两者都是曲包压缩包）。";
            }

            if (exception is IOException)
            {
                return "无法读写曲包文件，请检查文件是否被占用。";
            }

            return "导入失败：" + exception.Message;
        }
    }
}
