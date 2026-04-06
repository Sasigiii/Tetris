using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

[CustomEditor(typeof(UIButton), true)]
[CanEditMultipleObjects]
public class UIButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    [MenuItem("GameObject/UI/UIButton", false, 2031)]
    private static void CreateUIButton(MenuCommand menuCommand)
    {
        var go = new GameObject("UIButton");

        var rectTransform = go.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(160f, 30f);

        var image = go.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        go.AddComponent<UIButton>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var tmpText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.text = "Button";
        tmpText.alignment = TMPro.TextAlignmentOptions.Center;
        tmpText.color = new Color(50f / 255f, 50f / 255f, 50f / 255f);

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Font/AlibabaPuHuiTi-3-65-Medium SDF.asset");
        if (fontAsset != null)
            tmpText.font = fontAsset;

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

        if (go.transform.parent == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }
            go.transform.SetParent(canvas.transform, false);
        }

        Undo.RegisterCreatedObjectUndo(go, "Create UIButton");
        Selection.activeGameObject = go;
    }
}
