using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndScreenUI : MonoBehaviour
{
    private GameObject root;
    private Button quitButton;

#if UNITY_WEBGL && !UNITY_EDITOR
    private const bool ShowQuitButton = false;
#else
    private const bool ShowQuitButton = true;
#endif

    public void Show(Action onQuit)
    {
        if (root == null)
        {
            Build(onQuit);
        }

        root.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        UiNavigation.ConfigureEventSystem();
        if (quitButton != null)
        {
            UiNavigation.Select(quitButton.gameObject);
        }
    }

    private void Update()
    {
        if (root == null || !root.activeInHierarchy || quitButton == null)
        {
            return;
        }

        UiNavigation.KeepSelection(quitButton.gameObject);
    }

    private void Build(Action onQuit)
    {
        GameObject canvasObject = CreateUiObject("EndScreenCanvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateUiObject("EndScreenRoot", canvasObject.transform);
        Stretch(root.GetComponent<RectTransform>());
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(1f, 1f, 1f, 1f);
        overlay.raycastTarget = true;

        GameObject titleObject = CreateUiObject("Title", root.transform);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);
        titleRect.sizeDelta = new Vector2(900f, 80f);
        TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = "Thanks for playing";
        title.fontSize = 48f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.black;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;

        GameObject subtitleObject = CreateUiObject("Subtitle", root.transform);
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.anchoredPosition = new Vector2(0f, 20f);
        subtitleRect.sizeDelta = new Vector2(800f, ShowQuitButton ? 40f : 80f);
        TextMeshProUGUI subtitle = subtitleObject.AddComponent<TextMeshProUGUI>();
        subtitle.text = ShowQuitButton
            ? "Your answers have been saved. You can close the game now."
            : "Your answers have been saved. You can close this browser tab now.";
        subtitle.fontSize = 22f;
        subtitle.color = new Color(0.2f, 0.2f, 0.2f);
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.enableWordWrapping = true;
        subtitle.raycastTarget = false;

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        GameObject buttonObject = CreateUiObject("QuitButton", root.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -70f);
        buttonRect.sizeDelta = new Vector2(200f, 44f);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.70f, 1f, 0f, 1f);
        quitButton = buttonObject.AddComponent<Button>();
        UiNavigation.StyleButton(quitButton);
        UiNavigation.SetExplicitNavigation(quitButton, null, null, null, null);
        quitButton.onClick.AddListener(() => onQuit?.Invoke());

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Quit";
        label.fontSize = 24f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
#endif
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
