using System;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Infrastructure.Packages;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The resource pack page's file picker and its error wording. Android uses the same
    /// NativeFilePicker ZIP flow as the library (the picker hands back a readable local copy);
    /// the editor opens a ZIP dialog for development. A cancelled pick reports null.
    /// </summary>
    internal static class PackImport
    {
        internal static void PickZip(Action<string> onPicked)
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
            string path = EditorUtility.OpenFilePanelWithFilters(
                "选择资源包（.zip）", string.Empty, new[] { "资源包", "zip", "所有文件", "*" });
            onPicked(string.IsNullOrEmpty(path) ? null : path);
#else
            onPicked(null);
#endif
        }

        /// <summary>What a failed import tells the player; keeps the back end's path, group or slot details.</summary>
        internal static string DescribeImport(Exception exception)
        {
            switch (exception)
            {
                case null:
                    return "导入失败。";
                case OperationCanceledException:
                    return "已取消导入。";
                case MaterialPackBusyException:
                    return "本局进行中，结束后再导入资源包。";
                case InvalidDataException invalid:
                    return "不是有效的资源包：" + invalid.Message;
                case FileNotFoundException:
                case DirectoryNotFoundException:
                    return "找不到所选文件，请重新选择。";
                case UnauthorizedAccessException:
                    return "没有读取该文件的权限。";
                case IOException io:
                    return io.Message.Contains("空间") ? "存储空间不足，无法安装资源包。" : "无法读取资源包文件：" + io.Message;
                case JsonException json:
                    return "资源包索引无法写入：" + json.Message;
                default:
                    Debug.LogWarning("[PackImport] " + exception);
                    return "导入失败：" + exception.Message;
            }
        }

        /// <summary>What a failed save, preset or uninstall tells the player; the last valid configuration stays.</summary>
        internal static string DescribeWrite(Exception exception)
        {
            switch (exception)
            {
                case null:
                    return "保存失败。";
                case MaterialPackBusyException:
                    return "本局进行中，结束后再修改资源包。";
                case MaterialPackConflictException:
                    return "资源包选择已在别处更新，已显示最新状态，请重新选择。";
                case InvalidDataException invalid:
                    return "无法保存，已保留之前的选择：" + invalid.Message;
                case UnauthorizedAccessException:
                    return "没有写入资源包设置的权限。";
                case IOException io:
                    return "无法写入资源包设置：" + io.Message;
                default:
                    Debug.LogWarning("[PackScreen] " + exception);
                    return "保存失败：" + exception.Message;
            }
        }

        /// <summary>Why the index could not be opened at all.</summary>
        internal static string DescribeOpen(Exception exception) => exception switch
        {
            JsonException or InvalidDataException => "资源包索引已损坏，无法读取：" + exception.Message,
            UnauthorizedAccessException => "没有读取资源包目录的权限。",
            _ => "无法读取已安装的资源包：" + exception.Message
        };
    }
}
