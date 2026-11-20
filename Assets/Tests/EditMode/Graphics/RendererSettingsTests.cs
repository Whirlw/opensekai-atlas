using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Graphics
{
    public class RendererSettingsTests
    {
        private static SerializedObject Load(string name)
        {
            string path = "Assets/Graphics/Rendering/" + name + ".asset";
            var objects = AssetDatabase.LoadAllAssetsAtPath(path);
            var asset = objects.OfType<ScriptableObject>().SingleOrDefault();
            Assert.That(asset, Is.Not.Null, path + " contents: " +
                string.Join(", ", objects.Select(item => item == null ? "missing" : item.GetType().FullName)));
            return new SerializedObject(asset);
        }

        [Test]
        public void LibraryRendererLoadsMasksAndPassOrder()
        {
            using var asset = Load("ScreenLayerUI_0");
            Assert.That(asset.FindProperty("blurSettings.blurLayerMask").intValue, Is.EqualTo(64));
            Assert.That(asset.FindProperty("blurSettings.uiLayerMask").intValue, Is.EqualTo(32));
            Assert.That(asset.FindProperty("passSettings.eventDrawBlurUI").intValue, Is.EqualTo(300));
            Assert.That(asset.FindProperty("passSettings.eventBlur").intValue, Is.EqualTo(500));
            Assert.That(asset.FindProperty("passSettings.eventDrawUI").intValue, Is.EqualTo(600));
            Assert.That(asset.FindProperty("areaCameraSettings.eventDrawUI").intValue, Is.EqualTo(450));
            Assert.That(asset.FindProperty("areaCameraSettings.eventCopyScreenToCapture").intValue, Is.EqualTo(500));
        }

        [Test]
        public void SceneRendererLoadsEyelashPassAndOutlineCurve()
        {
            using var draw = Load("Eyelash_1");
            var tags = draw.FindProperty("settings.shaderTagTypes");
            Assert.That(tags.arraySize, Is.EqualTo(1));
            Assert.That(tags.GetArrayElementAtIndex(0).intValue, Is.EqualTo(8));
            Assert.That(draw.FindProperty("settings.passEvent").intValue, Is.EqualTo(450));
            using var outline = Load("SekaiCharacterOutlineFeature_3");
            var curve = outline.FindProperty("settings.fovCurve").animationCurveValue;
            Assert.That(curve.keys[0].value, Is.EqualTo(27.812466f).Within(0.0001f));
        }
    }
}
