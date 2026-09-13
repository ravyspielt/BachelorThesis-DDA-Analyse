using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VersionReadyPopup : MonoBehaviour
{
    private GameObject root;
    private Button confirmButton;
    private Action onConfirmed;

    public void Show(Action confirmedCallback)
    {
        onConfirmed = confirmedCallback;

        if (root == null)
        {
            Build();
        }

        root.SetActive(true);
        UiNavigation.ConfigureEventSystem();
        UiNavigation.Select(confirmButton.gameObject);
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void Update()
    {
        if (root == null || !root.activeInHierarchy || confirmButton == null)
        {
            return;
        }

        UiNavigation.KeepSelection(confirmButton.gameObject);
    }

    private void Build()
    {
        GameObject canvasObject = CreateUiObject("VersionReadyCanvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateUiObject("VersionReadyRoot", canvasObject.transform);
        Stretch(root.GetComponent<RectTransform>());
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.62f);
        overlay.raycastTarget = true;

        GameObject panel = CreateUiObject("Panel", root.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 240f);
        panel.AddComponent<Image>().color = Color.white;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateText(panel.transform, "Get ready!", 36f, FontStyles.Bold, Color.black, 48f);
        CreateText(
            panel.transform,
            "The next round starts now.",
            20f,
            FontStyles.Normal,
            new Color(0.2f, 0.2f, 0.2f),
            36f);

        GameObject buttonObject = CreateUiObject("ConfirmButton", panel.transform);
        LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 48f;
        buttonLayout.minHeight = 48f;
        buttonObject.AddComponent<Image>().color = new Color(0.70f, 1f, 0f, 1f);
        confirmButton = buttonObject.AddComponent<Button>();
        UiNavigation.StyleButton(confirmButton);
        UiNavigation.SetExplicitNavigation(confirmButton, null, null, null, null);
        confirmButton.onClick.AddListener(Confirm);

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Go";
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void Confirm()
    {
        Hide();
        onConfirmed?.Invoke();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string value, float fontSize, FontStyles style, Color color, float height)
    {
        GameObject textObject = CreateUiObject("Text", parent);
        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
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
