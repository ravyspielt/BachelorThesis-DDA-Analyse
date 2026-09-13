using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GeqQuestionnaireUI : MonoBehaviour
{
    private const int RadioColumnWidth = 92;
    private const int QuestionRowHeight = 64;
    private const int RadioSize = 28;

    private Canvas canvas;
    private GameObject root;
    private RectTransform content;
    private TextMeshProUGUI statusText;
    private Button continueButton;
    private Image continueButtonImage;
    private CanvasGroup continueButtonGroup;
    private Sprite circleSprite;
    private ScrollRect scrollRect;
    private GameObject lastSelected;
    private bool wasComplete;

    private readonly List<QuestionBinding> questions = new List<QuestionBinding>();
    private Action<GeqQuestionnaireResult> onCompleted;
    private string sessionId;
    private string condition;

    private class QuestionBinding
    {
        public GeqItem item;
        public Toggle[] toggles;
        public Image rowBackground;
        public int? selected;
    }

    public void Show(string sessionIdValue, string conditionValue, Action<GeqQuestionnaireResult> completedCallback)
    {
        sessionId = sessionIdValue;
        condition = conditionValue;
        onCompleted = completedCallback;

        if (root == null)
        {
            Build();
        }

        ResetAnswers();
        root.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        UiNavigation.ConfigureEventSystem();
        UiNavigation.Select(GetDefaultSelection());
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
        circleSprite = CreateCircleSprite(64);

        GameObject canvasObject = CreateUiObject("GeqCanvas", transform);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateUiObject("GeqRoot", canvasObject.transform);
        Stretch(root.GetComponent<RectTransform>());

        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.82f);
        overlay.raycastTarget = true;

        GameObject panel = CreateUiObject("Panel", root.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.06f, 0.05f);
        panelRect.anchorMax = new Vector2(0.94f, 0.95f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.97f, 0.97f, 0.98f, 1f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(36, 36, 28, 24);
        panelLayout.spacing = 12f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        CreateText(panel.transform, "Title", "In-game GEQ", 34f, FontStyles.Bold, new Color(0.12f, 0.14f, 0.18f), TextAlignmentOptions.Center, 44f);
        CreateText(
            panel.transform,
            "Instruction",
            "Please indicate how you felt while playing the game for each of the items.\nController: Stick or D-Pad to move, A to select.",
            20f,
            FontStyles.Normal,
            new Color(0.22f, 0.24f, 0.28f),
            TextAlignmentOptions.Center,
            64f);

        CreateScaleHeader(panel.transform);

        GameObject scrollObject = CreateUiObject("ScrollView", panel.transform);
        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 360f;

        Image scrollImage = scrollObject.AddComponent<Image>();
        scrollImage.color = new Color(0.93f, 0.94f, 0.96f, 1f);
        scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = CreateUiObject("Content", viewport.transform);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(16, 16, 12, 20);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content;

        for (int i = 0; i < GeqCatalog.Items.Length; i++)
        {
            CreateQuestionRow(content, GeqCatalog.Items[i], i);
        }

        statusText = CreateText(
            panel.transform,
            "Status",
            "Please answer every statement to continue.",
            18f,
            FontStyles.Italic,
            new Color(0.45f, 0.2f, 0.2f),
            TextAlignmentOptions.Center,
            28f);

        continueButton = CreateContinueButton(panel.transform);
        continueButton.onClick.AddListener(Submit);
        WireNavigation();
        RefreshStatus();
    }

    private void Update()
    {
        if (root == null || !root.activeInHierarchy)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            UiNavigation.Select(GetDefaultSelection());
            return;
        }

        if (selected != lastSelected)
        {
            lastSelected = selected;
            RectTransform visible = selected.GetComponent<RectTransform>();
            if (selected.transform.parent != null)
            {
                RectTransform parentRect = selected.transform.parent.GetComponent<RectTransform>();
                if (parentRect != null && parentRect.name.StartsWith("Question_"))
                {
                    visible = parentRect;
                }
            }

            if (visible != null)
            {
                UiNavigation.EnsureVisible(scrollRect, visible);
            }
        }
    }

    private GameObject GetDefaultSelection()
    {
        for (int i = 0; i < questions.Count; i++)
        {
            if (!HasAnswer(questions[i]) && questions[i].toggles[0] != null)
            {
                return questions[i].toggles[0].gameObject;
            }
        }

        if (continueButton != null && continueButton.interactable)
        {
            return continueButton.gameObject;
        }

        if (questions.Count > 0 && questions[0].toggles[0] != null)
        {
            return questions[0].toggles[0].gameObject;
        }

        return continueButton != null ? continueButton.gameObject : null;
    }

    private void WireNavigation()
    {
        for (int questionIndex = 0; questionIndex < questions.Count; questionIndex++)
        {
            Toggle[] toggles = questions[questionIndex].toggles;
            for (int score = 0; score < toggles.Length; score++)
            {
                Selectable up = questionIndex > 0 ? questions[questionIndex - 1].toggles[score] : null;
                Selectable down = questionIndex < questions.Count - 1 ? questions[questionIndex + 1].toggles[score] : null;
                Selectable left = score > 0 ? toggles[score - 1] : null;
                Selectable right = score < toggles.Length - 1 ? toggles[score + 1] : null;
                UiNavigation.SetExplicitNavigation(toggles[score], up, down, left, right);
            }
        }

        RefreshContinueNavigation();
    }

    private void RefreshContinueNavigation()
    {
        if (questions.Count == 0 || continueButton == null)
        {
            return;
        }

        Toggle[] lastRow = questions[questions.Count - 1].toggles;
        Selectable downTarget = continueButton.interactable ? continueButton : null;
        for (int score = 0; score < lastRow.Length; score++)
        {
            Selectable up = questions.Count > 1 ? questions[questions.Count - 2].toggles[score] : null;
            Selectable left = score > 0 ? lastRow[score - 1] : null;
            Selectable right = score < lastRow.Length - 1 ? lastRow[score + 1] : null;
            UiNavigation.SetExplicitNavigation(lastRow[score], up, downTarget, left, right);
        }

        Selectable continueUp = lastRow.Length > 2 ? lastRow[2] : lastRow[0];
        UiNavigation.SetExplicitNavigation(continueButton, continueUp, null, null, null);
    }

    private void CreateScaleHeader(Transform parent)
    {
        GameObject header = CreateUiObject("ScaleHeader", parent);
        LayoutElement headerLayout = header.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 58f;
        headerLayout.minHeight = 58f;
        header.AddComponent<Image>().color = new Color(0.88f, 0.90f, 0.94f, 1f);

        HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 8, 6, 6);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        GameObject label = CreateUiObject("HeaderLabel", header.transform);
        LayoutElement labelLayout = label.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minWidth = 220f;
        TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
        ConfigureText(labelText, "Statement", 16f, FontStyles.Bold, new Color(0.2f, 0.22f, 0.26f), TextAlignmentOptions.MidlineLeft);

        for (int i = 0; i < GeqCatalog.LikertAnchors.Length; i++)
        {
            GameObject column = CreateUiObject("Anchor_" + i, header.transform);
            LayoutElement columnLayout = column.AddComponent<LayoutElement>();
            columnLayout.preferredWidth = RadioColumnWidth;
            columnLayout.minWidth = RadioColumnWidth;
            TextMeshProUGUI columnText = column.AddComponent<TextMeshProUGUI>();
            ConfigureText(columnText, GeqCatalog.LikertAnchors[i], 14f, FontStyles.Bold, new Color(0.2f, 0.22f, 0.26f), TextAlignmentOptions.Center);
            columnText.enableWordWrapping = true;
        }
    }

    private void CreateQuestionRow(Transform parent, GeqItem item, int index)
    {
        GameObject row = CreateUiObject("Question_" + item.itemKey, parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = QuestionRowHeight;
        rowLayout.minHeight = QuestionRowHeight;

        Image background = row.AddComponent<Image>();
        background.color = index % 2 == 0
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(0.95f, 0.96f, 0.98f, 1f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 8, 8, 8);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        GameObject questionObject = CreateUiObject("QuestionText", row.transform);
        LayoutElement questionLayout = questionObject.AddComponent<LayoutElement>();
        questionLayout.flexibleWidth = 1f;
        questionLayout.minWidth = 220f;
        TextMeshProUGUI questionText = questionObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(questionText, (index + 1) + ". " + item.text, 18f, FontStyles.Normal, new Color(0.12f, 0.14f, 0.18f), TextAlignmentOptions.MidlineLeft);
        questionText.enableWordWrapping = true;

        ToggleGroup group = row.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;

        QuestionBinding binding = new QuestionBinding
        {
            item = item,
            toggles = new Toggle[GeqCatalog.LikertMax - GeqCatalog.LikertMin + 1],
            rowBackground = background,
            selected = null
        };

        for (int score = GeqCatalog.LikertMin; score <= GeqCatalog.LikertMax; score++)
        {
            binding.toggles[score] = CreateRadio(row.transform, group, binding, score);
            binding.toggles[score].SetIsOnWithoutNotify(false);
        }

        questions.Add(binding);
    }

    private Toggle CreateRadio(Transform parent, ToggleGroup group, QuestionBinding binding, int score)
    {
        GameObject cell = CreateUiObject("Radio_" + score, parent);
        LayoutElement cellLayout = cell.AddComponent<LayoutElement>();
        cellLayout.preferredWidth = RadioColumnWidth;
        cellLayout.minWidth = RadioColumnWidth;
        Image hitArea = cell.AddComponent<Image>();
        hitArea.color = Color.clear;

        Toggle toggle = cell.AddComponent<Toggle>();
        toggle.group = group;
        toggle.isOn = false;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        ColorBlock toggleColors = toggle.colors;
        toggleColors.normalColor = Color.white;
        toggleColors.highlightedColor = new Color(1f, 0.92f, 0.45f, 1f);
        toggleColors.selectedColor = new Color(1f, 0.82f, 0.15f, 1f);
        toggleColors.pressedColor = new Color(0.85f, 0.7f, 0.15f, 1f);
        toggle.colors = toggleColors;

        GameObject backgroundObject = CreateUiObject("Background", cell.transform);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(RadioSize, RadioSize);
        Image background = backgroundObject.AddComponent<Image>();
        background.sprite = circleSprite;
        background.color = new Color(0.72f, 0.74f, 0.78f, 1f);
        toggle.targetGraphic = background;

        GameObject checkObject = CreateUiObject("Checkmark", backgroundObject.transform);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(6f, 6f);
        checkRect.offsetMax = new Vector2(-6f, -6f);
        Image check = checkObject.AddComponent<Image>();
        check.sprite = circleSprite;
        check.color = new Color(0.16f, 0.42f, 0.78f, 1f);
        toggle.graphic = check;

        toggle.onValueChanged.AddListener(isOn =>
        {
            if (isOn)
            {
                binding.selected = score;
            }
            else if (binding.selected == score)
            {
                binding.selected = null;
            }

            RefreshStatus();
        });

        return toggle;
    }

    private Button CreateContinueButton(Transform parent)
    {
        GameObject buttonObject = CreateUiObject("ContinueButton", parent);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 56f;
        layout.minHeight = 56f;
        layout.preferredWidth = 280f;

        continueButtonImage = buttonObject.AddComponent<Image>();
        continueButtonImage.color = new Color(0.55f, 0.58f, 0.62f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = false;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.22f, 0.50f, 0.86f, 1f);
        colors.selectedColor = new Color(1f, 0.82f, 0.15f, 1f);
        colors.pressedColor = new Color(0.12f, 0.32f, 0.62f, 1f);
        colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;

        continueButtonGroup = buttonObject.AddComponent<CanvasGroup>();
        continueButtonGroup.alpha = 0.55f;
        continueButtonGroup.interactable = false;
        continueButtonGroup.blocksRaycasts = false;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(label, "Continue", 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        return button;
    }

    private void ResetAnswers()
    {
        for (int i = 0; i < questions.Count; i++)
        {
            QuestionBinding binding = questions[i];
            binding.selected = null;
            binding.rowBackground.color = i % 2 == 0
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.95f, 0.96f, 0.98f, 1f);

            for (int t = 0; t < binding.toggles.Length; t++)
            {
                binding.toggles[t].SetIsOnWithoutNotify(false);
            }
        }

        if (content != null)
        {
            content.anchoredPosition = Vector2.zero;
        }

        wasComplete = false;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        int remaining = UnansweredCount();
        bool complete = remaining == 0;

        if (statusText != null)
        {
            statusText.text = complete
                ? "All statements answered."
                : remaining == 1
                    ? "1 statement still needs an answer."
                    : remaining + " statements still need an answer.";
            statusText.color = complete
                ? new Color(0.16f, 0.45f, 0.28f)
                : new Color(0.55f, 0.22f, 0.22f);
        }

        if (continueButton != null)
        {
            continueButton.interactable = complete;
        }

        if (continueButtonImage != null)
        {
            continueButtonImage.color = complete
                ? new Color(0.16f, 0.42f, 0.78f, 1f)
                : new Color(0.55f, 0.58f, 0.62f, 1f);
        }

        if (continueButtonGroup != null)
        {
            continueButtonGroup.alpha = complete ? 1f : 0.55f;
            continueButtonGroup.interactable = complete;
            continueButtonGroup.blocksRaycasts = complete;
        }

        RefreshContinueNavigation();

        if (complete && !wasComplete && continueButton != null)
        {
            UiNavigation.Select(continueButton.gameObject);
        }

        wasComplete = complete;
    }

    private int UnansweredCount()
    {
        int remaining = 0;

        for (int i = 0; i < questions.Count; i++)
        {
            if (!HasAnswer(questions[i]))
            {
                remaining++;
            }
        }

        return remaining;
    }

    private static bool HasAnswer(QuestionBinding binding)
    {
        if (!binding.selected.HasValue)
        {
            return false;
        }

        int selected = binding.selected.Value;
        if (selected < 0 || selected >= binding.toggles.Length)
        {
            return false;
        }

        return binding.toggles[selected] != null && binding.toggles[selected].isOn;
    }

    private void Submit()
    {
        if (UnansweredCount() > 0)
        {
            HighlightUnanswered();
            RefreshStatus();
            return;
        }

        GeqItemAnswer[] answers = new GeqItemAnswer[questions.Count];

        for (int i = 0; i < questions.Count; i++)
        {
            QuestionBinding binding = questions[i];
            answers[i] = new GeqItemAnswer
            {
                itemKey = binding.item.itemKey,
                subscale = binding.item.subscale,
                text = binding.item.text,
                score = binding.selected.Value
            };
        }

        GeqQuestionnaireResult result = new GeqQuestionnaireResult
        {
            sessionId = sessionId,
            condition = condition,
            completedAtIso = DateTime.UtcNow.ToString("o"),
            items = answers,
            subscaleMeans = GeqCatalog.ComputeMeans(answers)
        };

        Hide();
        onCompleted?.Invoke(result);
    }

    private void HighlightUnanswered()
    {
        for (int i = 0; i < questions.Count; i++)
        {
            if (HasAnswer(questions[i]))
            {
                continue;
            }

            questions[i].rowBackground.color = new Color(1f, 0.86f, 0.86f, 1f);
        }
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment,
        float height)
    {
        GameObject textObject = CreateUiObject(name, parent);
        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(text, value, fontSize, style, color, alignment);
        return text;
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        string value,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment)
    {
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
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

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float radius = size * 0.5f - 1f;
        float radiusSquared = radius * radius;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                pixels[y * size + x] = dx * dx + dy * dy <= radiusSquared
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
