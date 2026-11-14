using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// Puts the Game view on a fixed resolution so Screen.width/height, the canvas scalers and the
    /// captured PNG all have the size asked for. Unity has no public API for this; the internal
    /// GameViewSizes table is reached by reflection (stable since 2019, checked on 6000.6).
    /// </summary>
    internal static class GameViewResolution
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string SizePrefix = "OpenSekai Shot ";

        private static Assembly EditorAssembly => typeof(Editor).Assembly;

        /// <summary>Selects (adding it when missing) a fixed-resolution entry and returns the previous index.</summary>
        internal static int Apply(int width, int height)
        {
            Type gameViewType = EditorAssembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            object group = CurrentGroup();
            int index = FindOrAdd(group, width, height);
            PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", Any);
            int previous = (int)selected.GetValue(gameView);
            selected.SetValue(gameView, index);
            gameView.Repaint();
            return previous;
        }

        internal static void Restore(int index)
        {
            if (index < 0)
            {
                return;
            }
            Type gameViewType = EditorAssembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameViewType.GetProperty("selectedSizeIndex", Any).SetValue(gameView, index);
            gameView.Repaint();
        }

        private static object CurrentGroup()
        {
            Type sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            return sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        }

        private static int FindOrAdd(object group, int width, int height)
        {
            Type groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
            Type sizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
            Type sizeKind = EditorAssembly.GetType("UnityEditor.GameViewSizeType");
            object fixedKind = Enum.Parse(sizeKind, "FixedResolution");
            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if (Equals(sizeType.GetProperty("sizeType").GetValue(size), fixedKind)
                    && (int)sizeType.GetProperty("width").GetValue(size) == width
                    && (int)sizeType.GetProperty("height").GetValue(size) == height)
                {
                    return i;
                }
            }

            object created = Activator.CreateInstance(sizeType, fixedKind, width, height, SizePrefix + width + "x" + height);
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { created });
            return total;
        }
    }
}
