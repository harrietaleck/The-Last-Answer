using NUnit.Framework;
using TLA.UI.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.Tests
{
    /// <summary>
    /// Menu rows used to ignore clicks: the plate was fully transparent, Unity dropped it from
    /// the canvas, and the label only received clicks on the glyphs.
    /// </summary>
    public class MenuButtonHitTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Components/UI_MenuButton.prefab";

        [Test]
        public void MenuButton_PlateStaysClickable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "UI_MenuButton prefab missing.");

            var canvas = new GameObject("MenuButtonTestCanvas", typeof(Canvas));
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                var image = instance.GetComponent<Image>();
                var style = instance.GetComponent<UIButtonStyle>();

                Assert.IsNotNull(image);
                Assert.IsNotNull(style);
                Assert.IsTrue(image.raycastTarget, "The row plate must receive pointer clicks.");
                Assert.Greater(image.color.a, 0f, "An alpha of 0 is omitted from the canvas, so the row cannot be clicked.");
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
