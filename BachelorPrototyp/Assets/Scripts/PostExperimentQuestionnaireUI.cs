using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PostExperimentQuestionnaireUI : MonoBehaviour
{
    private const int RadioColumnWidth = 92;
    private const int LikertRowHeight = 78;
    private const int RadioSize = 28;
    private const int ChoiceOptionHeight = 44;

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
    private Action<PostExperimentResult> onCompleted;
    private string sessionId;
    private string[] playOrder;

    private class QuestionBinding
    {
        public PostExperimentItem item;
        public Toggle[] toggles;
        public Image rowBackground;
        public int? selected;
    }

    public void Show(string sessionIdValue, string[] playOrderValue, Action<PostExperimentResult> completedCallback)
    {
        sessionId = sessionIdValue;
        playOrder = playOrderValue ?? new string[0];
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

        GameObject canvasObject = CreateUiObject("PostExperimentCanvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateUiObject("PostExperimentRoot", canvasObject.transform);
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
        panel.AddComponent<Image>().color = new Color(0.97f, 0.97f, 0.98f, 1f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(36, 36, 28, 24);
        panelLayout.spacing = 12f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        CreateText(panel.transform, "Title", "Final questionnaire", 34f, FontStyles.Bold, new Color(0.12f, 0.14f, 0.18f), TextAlignmentOptions.Center, 44f);
        CreateText(
            panel.transform,
            "Instruction",
            "Please think about all three main rounds together (not the warm-up).\nController: Stick or D-Pad to move, A to select.",
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
        scrollObject.AddComponent<Image>().color = new Color(0.93f, 0.94f, 0.96f, 1f);
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
        contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content;

        int displayIndex = 0;
        for (int i = 0; i < PostExperimentCatalog.Items.Length; i++)
        {
            PostExperimentItem item = PostExperimentCatalog.Items[i];
            if (item.type == PostQuestionType.Choice)
            {
                CreateChoiceRow(content, item, displayIndex);
            }
            else
            {
                CreateLikertRow(content, item, displayIndex);
            }

            displayIndex++;
        }

        statusText = CreateText(
            panel.transform,
            "Status",
            "Please answer every question to continue.",
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
            Transform walk = selected.transform;
            while (walk != null && walk.parent != content)
            {
                walk = walk.parent;
            }

            if (walk != null)
            {
                visible = walk.GetComponent<RectTransform>();
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
            if (!HasAnswer(questions[i]) && questions[i].toggles.Length > 0 && questions[i].toggles[0] != null)
            {
                return questions[i].toggles[0].gameObject;
            }
        }

        if (continueButton != null && continueButton.interactable)
        {
            return continueButton.gameObject;
        }

        return questions.Count > 0 ? questions[0].toggles[0].gameObject : continueButton.gameObject;
    }

    private void WireNavigation()
    {
        for (int questionIndex = 0; questionIndex < questions.Count; questionIndex++)
        {
            QuestionBinding binding = questions[questionIndex];
            Toggle[] toggles = binding.toggles;
            bool likert = binding.item.type == PostQuestionType.Likert;

            for (int i = 0; i < toggles.Length; i++)
            {
                Selectable up;
                Selectable down;
                Selectable left = null;
                Selectable right = null;

                if (likert)
                {
                    up = GetAlignedToggle(questionIndex - 1, i);
                    down = GetAlignedToggle(questionIndex + 1, i);
                    left = i > 0 ? toggles[i - 1] : null;
                    right = i < toggles.Length - 1 ? toggles[i + 1] : null;
                }
                else
                {
                    if (i > 0)
                    {
                        up = toggles[i - 1];
                    }
                    else
                    {
                        up = GetLastToggle(questionIndex - 1);
                    }

                    if (i < toggles.Length - 1)
                    {
                        down = toggles[i + 1];
                    }
                    else
                    {
                        down = GetFirstToggle(questionIndex + 1);
                    }
                }

                UiNavigation.SetExplicitNavigation(toggles[i], up, down, left, right);
            }
        }

        RefreshContinueNavigation();
    }

    private Selectable GetAlignedToggle(int questionIndex, int score)
    {
        if (questionIndex < 0 || questionIndex >= questions.Count)
        {
            return questionIndex >= questions.Count ? continueButton : null;
        }

        Toggle[] toggles = questions[questionIndex].toggles;
        if (questions[questionIndex].item.type == PostQuestionType.Choice)
        {
            return questionIndex < questions.Count ? (score == 0 ? toggles[0] : toggles[toggles.Length - 1]) : continueButton;
        }

        int index = Mathf.Clamp(score, 0, toggles.Length - 1);
        return toggles[index];
    }

    private Selectable GetFirstToggle(int questionIndex)
    {
        if (questionIndex >= questions.Count)
        {
            return continueButton != null && continueButton.interactable ? continueButton : null;
        }

        if (questionIndex < 0)
        {
            return null;
        }

        return questions[questionIndex].toggles[0];
    }

    private Selectable GetLastToggle(int questionIndex)
    {
        if (questionIndex < 0)
        {
            return null;
        }

        Toggle[] toggles = questions[questionIndex].toggles;
        return toggles[toggles.Length - 1];
    }

    private void RefreshContinueNavigation()
    {
        if (questions.Count == 0 || continueButton == null)
        {
            return;
        }

        QuestionBinding last = questions[questions.Count - 1];
        Selectable downTarget = continueButton.interactable ? continueButton : null;
        Toggle[] lastToggles = last.toggles;

        if (last.item.type == PostQuestionType.Likert)
        {
            for (int i = 0; i < lastToggles.Length; i++)
            {
                Selectable up = GetAlignedToggle(questions.Count - 2, i);
                Selectable left = i > 0 ? lastToggles[i - 1] : null;
                Selectable right = i < lastToggles.Length - 1 ? lastToggles[i + 1] : null;
                UiNavigation.SetExplicitNavigation(lastToggles[i], up, downTarget, left, right);
            }

            Selectable continueUp = lastToggles.Length > 2 ? lastToggles[2] : lastToggles[0];
            UiNavigation.SetExplicitNavigation(continueButton, continueUp, null, null, null);
        }
        else
        {
            int lastIndex = lastToggles.Length - 1;
            Selectable up = lastIndex > 0 ? lastToggles[lastIndex - 1] : GetLastToggle(questions.Count - 2);
            UiNavigation.SetExplicitNavigation(lastToggles[lastIndex], up, downTarget, null, null);
            UiNavigation.SetExplicitNavigation(continueButton, lastToggles[lastIndex], null, null, null);
        }
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

        for (int i = 0; i < PostExperimentCatalog.LikertAnchors.Length; i++)
        {
            GameObject column = CreateUiObject("Anchor_" + i, header.transform);
            LayoutElement columnLayout = column.AddComponent<LayoutElement>();
            columnLayout.preferredWidth = RadioColumnWidth;
            columnLayout.minWidth = RadioColumnWidth;
            TextMeshProUGUI columnText = column.AddComponent<TextMeshProUGUI>();
            ConfigureText(columnText, PostExperimentCatalog.LikertAnchors[i], 14f, FontStyles.Bold, new Color(0.2f, 0.22f, 0.26f), TextAlignmentOptions.Center);
            columnText.enableWordWrapping = true;
        }
    }

    private void CreateLikertRow(Transform parent, PostExperimentItem item, int index)
    {
        GameObject row = CreateUiObject("Question_" + item.itemKey, parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = LikertRowHeight;
        rowLayout.minHeight = LikertRowHeight;

        Image background = row.AddComponent<Image>();
        background.color = EvenColor(index);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 8, 8, 8);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        CreateQuestionLabel(row.transform, (index + 1) + ". " + item.text);

        ToggleGroup group = row.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;

        QuestionBinding binding = new QuestionBinding
        {
            item = item,
            toggles = new Toggle[PostExperimentCatalog.LikertMax - PostExperimentCatalog.LikertMin + 1],
            rowBackground = background,
            selected = null
        };

        for (int score = PostExperimentCatalog.LikertMin; score <= PostExperimentCatalog.LikertMax; score++)
        {
            binding.toggles[score] = CreateRadio(row.transform, group, binding, score);
            binding.toggles[score].SetIsOnWithoutNotify(false);
        }

        questions.Add(binding);
    }

    private void CreateChoiceRow(Transform parent, PostExperimentItem item, int index)
    {
        GameObject row = CreateUiObject("Question_" + item.itemKey, parent);
        int optionCount = item.options != null ? item.options.Length : 0;
        int height = 56 + optionCount * ChoiceOptionHeight;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = height;
        rowLayout.minHeight = height;

        Image background = row.AddComponent<Image>();
        background.color = EvenColor(index);

        VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateQuestionLabel(row.transform, (index + 1) + ". " + item.text, 40f);

        ToggleGroup group = row.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;

        QuestionBinding binding = new QuestionBinding
        {
            item = item,
            toggles = new Toggle[optionCount],
            rowBackground = background,
            selected = null
        };

        for (int i = 0; i < optionCount; i++)
        {
            binding.toggles[i] = CreateChoiceOption(row.transform, group, binding, i, item.options[i].text);
            binding.toggles[i].SetIsOnWithoutNotify(false);
        }

        questions.Add(binding);
    }

    private void CreateQuestionLabel(Transform parent, string value, float minHeight = 0f)
    {
        GameObject questionObject = CreateUiObject("QuestionText", parent);
        LayoutElement questionLayout = questionObject.AddComponent<LayoutElement>();
        questionLayout.flexibleWidth = 1f;
        questionLayout.minWidth = 220f;
        if (minHeight > 0f)
        {
            questionLayout.minHeight = minHeight;
            questionLayout.preferredHeight = minHeight;
        }

        TextMeshProUGUI questionText = questionObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(questionText, value, 18f, FontStyles.Normal, new Color(0.12f, 0.14f, 0.18f), TextAlignmentOptions.MidlineLeft);
        questionText.enableWordWrapping = true;
    }

    private Toggle CreateChoiceOption(Transform parent, ToggleGroup group, QuestionBinding binding, int optionIndex, string optionText)
    {
        GameObject option = CreateUiObject("Option_" + optionIndex, parent);
        LayoutElement optionLayout = option.AddComponent<LayoutElement>();
        optionLayout.preferredHeight = ChoiceOptionHeight;
        optionLayout.minHeight = ChoiceOptionHeight;
        Image hitArea = option.AddComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0.01f);

        HorizontalLayoutGroup layout = option.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        Toggle toggle = option.AddComponent<Toggle>();
        toggle.group = group;
        toggle.isOn = false;
        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.45f, 1f);
        colors.selectedColor = new Color(1f, 0.82f, 0.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.7f, 0.15f, 1f);
        toggle.colors = colors;

        GameObject radioCell = CreateUiObject("Radio", option.transform);
        LayoutElement radioLayout = radioCell.AddComponent<LayoutElement>();
        radioLayout.preferredWidth = 40f;
        radioLayout.minWidth = 40f;

        GameObject backgroundObject = CreateUiObject("Background", radioCell.transform);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(RadioSize, RadioSize);
        Image background = backgroundObject.AddComponent<Image>();
        background.sprite = circleSprite;
        background.color = new Color(0.72f, 0.74f, 0.78f, 1f);
        toggle.targetGraphic = hitArea;

        GameObject checkObject = CreateUiObject("Checkmark", backgroundObject.transform);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(6f, 6f);
        checkRect.offsetMax = new Vector2(-6f, -6f);
        Image check = checkObject.AddComponent<Image>();
        check.sprite = circleSprite;
        check.color = new Color(0.16f, 0.42f, 0.78f, 1f);
        toggle.graphic = check;

        GameObject labelObject = CreateUiObject("Label", option.transform);
        LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minWidth = 200f;
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(label, optionText, 18f, FontStyles.Normal, new Color(0.12f, 0.14f, 0.18f), TextAlignmentOptions.MidlineLeft);

        toggle.onValueChanged.AddListener(isOn =>
        {
            if (isOn)
            {
                binding.selected = optionIndex;
            }
            else if (binding.selected == optionIndex)
            {
                binding.selected = null;
            }

            RefreshStatus();
        });

        return toggle;
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
            binding.rowBackground.color = EvenColor(i);

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
                ? "All questions answered."
                : remaining == 1
                    ? "1 question still needs an answer."
                    : remaining + " questions still need an answer.";
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

        PostExperimentAnswer[] answers = new PostExperimentAnswer[questions.Count];
        for (int i = 0; i < questions.Count; i++)
        {
            QuestionBinding binding = questions[i];
            int selected = binding.selected.Value;
            string optionKey = "";
            string optionText = "";

            if (binding.item.type == PostQuestionType.Choice && binding.item.options != null && selected < binding.item.options.Length)
            {
                optionKey = binding.item.options[selected].optionKey;
                optionText = binding.item.options[selected].text;
            }

            answers[i] = new PostExperimentAnswer
            {
                itemKey = binding.item.itemKey,
                construct = binding.item.construct,
                text = binding.item.text,
                score = selected,
                optionKey = optionKey,
                optionText = optionText
            };
        }

        PostExperimentResult result = new PostExperimentResult
        {
            sessionId = sessionId,
            completedAtIso = DateTime.UtcNow.ToString("o"),
            playOrder = playOrder,
            answers = answers
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

    private static Color EvenColor(int index)
    {
        return index % 2 == 0
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(0.95f, 0.96f, 0.98f, 1f);
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
