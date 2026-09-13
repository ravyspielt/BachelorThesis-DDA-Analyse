using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseOverlay : MonoBehaviour
{
    private GameObject root;

    public void Show()
    {
        if (root == null)
        {
            Build();
        }

        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void Build()
    {
        GameObject canvasObject = CreateUiObject("PauseCanvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateUiObject("PauseRoot", canvasObject.transform);
        Stretch(root.GetComponent<RectTransform>());
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.55f);
        overlay.raycastTarget = true;

        GameObject titleObject = CreateUiObject("Title", root.transform);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(800f, 120f);
        TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = "Pause";
        title.fontSize = 72f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;

        GameObject hintObject = CreateUiObject("Hint", root.transform);
        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -70f);
        hintRect.sizeDelta = new Vector2(800f, 40f);
        TextMeshProUGUI hint = hintObject.AddComponent<TextMeshProUGUI>();
        hint.text = "Press P or the Menu button to continue";
        hint.fontSize = 20f;
        hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
