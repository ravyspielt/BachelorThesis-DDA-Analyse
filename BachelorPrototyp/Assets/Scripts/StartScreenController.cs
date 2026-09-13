using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private TMP_InputField ageInput;
    private TMP_InputField hoursInput;
    private Toggle consentToggle;
    private TextMeshProUGUI screeningHint;
    private GameObject tutorialPopup;
    private Button tutorialConfirmButton;
    private CanvasGroup menuGroup;

    private void Awake()
    {
        CreateScreeningFields();
        CreateConsentBlock();
        CreateTutorialPopup();
        CacheMenuGroup();
        ConfigureMenuNavigation();

        if (startButton != null)
        {
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchoredPosition = new Vector2(0f, -118f);
            startButton.onClick.AddListener(ShowTutorial);
        }

        if (quitButton != null)
        {
            RectTransform quitRect = quitButton.GetComponent<RectTransform>();
            quitRect.anchoredPosition = new Vector2(0f, -164f);
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void Start()
    {
        UiNavigation.ConfigureEventSystem();
        if (startButton != null)
        {
            UiNavigation.Select(startButton.gameObject);
        }
    }

    private void Update()
    {
        GameObject fallback = tutorialPopup != null && tutorialPopup.activeSelf && tutorialConfirmButton != null
            ? tutorialConfirmButton.gameObject
            : startButton != null ? startButton.gameObject : null;

        UiNavigation.KeepSelection(fallback);
        RefreshStartButton();
    }

    private void ShowTutorial()
    {
        if (!TryReadScreening(out _, out _) || consentToggle == null || !consentToggle.isOn)
        {
            SetHint("Please enter age, hours per week, and accept the consent.", true);
            return;
        }

        SetHint("", false);

        if (menuGroup != null)
        {
            menuGroup.interactable = false;
        }

        if (tutorialPopup != null)
        {
            tutorialPopup.SetActive(true);
        }

        if (tutorialConfirmButton != null)
        {
            UiNavigation.Select(tutorialConfirmButton.gameObject);
        }
    }

    private void StartGame()
    {
        if (tutorialPopup != null)
        {
            tutorialPopup.SetActive(false);
        }

        TryReadScreening(out int age, out int weeklyPlayHours);
        GameManager.Instance.BeginSession(age, weeklyPlayHours);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void CreateScreeningFields()
    {
        Transform parent = transform.Find("Panel");
        if (parent == null)
        {
            parent = transform;
        }

        ageInput = CreateNumberField(parent, "AgeField", "Age", new Vector2(-155f, 88f), 90f);
        hoursInput = CreateNumberField(parent, "HoursField", "Approximate play hours per week", new Vector2(80f, 88f), 280f);

        GameObject hintObject = CreateUiObject("ScreeningHint", parent);
        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -198f);
        hintRect.sizeDelta = new Vector2(520f, 36f);
        screeningHint = hintObject.AddComponent<TextMeshProUGUI>();
        screeningHint.text = "Age, hours per week and consent are required.";
        screeningHint.fontSize = 14f;
        screeningHint.color = new Color(0.35f, 0.35f, 0.35f);
        screeningHint.alignment = TextAlignmentOptions.Center;
        screeningHint.raycastTarget = false;
    }

    private void CreateConsentBlock()
    {
        Transform parent = transform.Find("Panel");
        if (parent == null)
        {
            parent = transform;
        }

        GameObject textObject = CreateUiObject("ConsentText", parent);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 18f);
        textRect.sizeDelta = new Vector2(560f, 96f);
        TextMeshProUGUI consentText = textObject.AddComponent<TextMeshProUGUI>();
        consentText.text =
            "This session is part of a bachelor's thesis. I store your age, weekly play time, gameplay stats and questionnaire answers under a random ID. No name is collected. Data is saved in Firebase (Google) and kept for this thesis and related academic research. Participation is voluntary; you can quit at any time.";
        consentText.fontSize = 13f;
        consentText.color = new Color(0.18f, 0.18f, 0.18f);
        consentText.alignment = TextAlignmentOptions.Center;
        consentText.enableWordWrapping = true;
        consentText.raycastTarget = false;

        GameObject row = CreateUiObject("ConsentRow", parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -52f);
        rowRect.sizeDelta = new Vector2(400f, 36f);

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        ContentSizeFitter rowFitter = row.AddComponent<ContentSizeFitter>();
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject boxSlot = CreateUiObject("ConsentBoxSlot", row.transform);
        LayoutElement slotLayout = boxSlot.AddComponent<LayoutElement>();
        slotLayout.preferredWidth = 22f;
        slotLayout.preferredHeight = 22f;
        slotLayout.minWidth = 22f;
        slotLayout.minHeight = 22f;
        slotLayout.flexibleWidth = 0f;
        slotLayout.flexibleHeight = 0f;

        GameObject boxObject = CreateUiObject("ConsentBox", boxSlot.transform);
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(22f, 22f);
        Image boxImage = boxObject.AddComponent<Image>();
        boxImage.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        GameObject checkObject = CreateUiObject("Check", boxObject.transform);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(4f, 4f);
        checkRect.offsetMax = new Vector2(-4f, -4f);
        Image checkImage = checkObject.AddComponent<Image>();
        checkImage.color = new Color(0.16f, 0.42f, 0.78f, 1f);

        consentToggle = row.AddComponent<Toggle>();
        consentToggle.isOn = false;
        consentToggle.targetGraphic = boxImage;
        consentToggle.graphic = checkImage;
        ColorBlock colors = consentToggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.45f, 1f);
        colors.selectedColor = new Color(1f, 0.82f, 0.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.7f, 0.15f, 1f);
        consentToggle.colors = colors;
        consentToggle.onValueChanged.AddListener(_ => RefreshStartButton());

        GameObject labelObject = CreateUiObject("ConsentLabel", row.transform);
        LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 270f;
        labelLayout.preferredHeight = 36f;
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "I agree to this data processing.";
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
    }

    private TMP_InputField CreateNumberField(Transform parent, string objectName, string labelText, Vector2 position, float width)
    {
        GameObject root = CreateUiObject(objectName, parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = new Vector2(width, 56f);

        GameObject labelObject = CreateUiObject("Label", root.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.55f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        GameObject inputObject = CreateUiObject("Input", root.transform);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0.55f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        Image inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        GameObject textArea = CreateUiObject("Text Area", inputObject.transform);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(8f, 4f);
        textAreaRect.offsetMax = new Vector2(-8f, -4f);
        textArea.AddComponent<RectMask2D>();

        GameObject placeholderObject = CreateUiObject("Placeholder", textArea.transform);
        Stretch(placeholderObject.GetComponent<RectTransform>());
        TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = "";
        placeholder.fontSize = 16f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.4f, 0.4f, 0.4f, 0.75f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject textObject = CreateUiObject("Text", textArea.transform);
        Stretch(textObject.GetComponent<RectTransform>());
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16f;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = text.font;
        input.pointSize = 16f;
        input.characterLimit = 3;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private void RefreshStartButton()
    {
        if (startButton == null || (tutorialPopup != null && tutorialPopup.activeSelf))
        {
            return;
        }

        startButton.interactable = TryReadScreening(out _, out _) && consentToggle != null && consentToggle.isOn;
    }

    private bool TryReadScreening(out int age, out int weeklyPlayHours)
    {
        age = 0;
        weeklyPlayHours = 0;

        if (ageInput == null || hoursInput == null)
        {
            return false;
        }

        if (!int.TryParse(ageInput.text.Trim(), out age) || age < 10 || age > 99)
        {
            return false;
        }

        if (!int.TryParse(hoursInput.text.Trim(), out weeklyPlayHours) || weeklyPlayHours < 0 || weeklyPlayHours > 200)
        {
            return false;
        }

        return true;
    }

    private void SetHint(string value, bool isError)
    {
        if (screeningHint == null)
        {
            return;
        }

        screeningHint.text = string.IsNullOrEmpty(value)
            ? "Age, hours per week and consent are required."
            : value;
        screeningHint.color = isError
            ? new Color(0.7f, 0.15f, 0.15f)
            : new Color(0.35f, 0.35f, 0.35f);
    }

    private void CreateTutorialPopup()
    {
        tutorialPopup = CreateUiObject("TutorialPopup", transform);
        Stretch(tutorialPopup.GetComponent<RectTransform>());
        Image dim = tutorialPopup.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = true;

        GameObject panel = CreateUiObject("TutorialPanel", tutorialPopup.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 500f);
        panel.AddComponent<Image>().color = Color.white;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreatePopupText(panel.transform, "Controls", 32f, FontStyles.Bold, Color.black, 40f);
        CreatePopupText(
            panel.transform,
            "After you click Continue, the warm-up starts. You will then play three rounds and fill in a short questionnaire after each one.",
            16f,
            FontStyles.Normal,
            new Color(0.2f, 0.2f, 0.2f),
            40f);

        GameObject columns = CreateUiObject("Columns", panel.transform);
        LayoutElement columnsLayout = columns.AddComponent<LayoutElement>();
        columnsLayout.preferredHeight = 250f;
        columnsLayout.minHeight = 250f;
        HorizontalLayoutGroup columnLayout = columns.AddComponent<HorizontalLayoutGroup>();
        columnLayout.spacing = 18f;
        columnLayout.childAlignment = TextAnchor.UpperCenter;
        columnLayout.childControlHeight = true;
        columnLayout.childControlWidth = true;
        columnLayout.childForceExpandHeight = true;
        columnLayout.childForceExpandWidth = true;

        CreateControlColumn(
            columns.transform,
            "Keyboard & Mouse",
            "Move: WASD\nAim: Mouse\nShoot: Space / Left click\nDash: Shift\nPause: P");
        CreateControlColumn(
            columns.transform,
            "Controller",
            "Move: Left stick\nAim: Right stick\nShoot: Right trigger\nDash: A button\nPause: Menu button");

        GameObject buttonObject = CreateUiObject("ConfirmButton", panel.transform);
        LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 44f;
        buttonLayout.minHeight = 44f;
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.70f, 1f, 0f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        UiNavigation.StyleButton(button);
        UiNavigation.SetExplicitNavigation(button, null, null, null, null);
        button.onClick.AddListener(StartGame);
        tutorialConfirmButton = button;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Continue";
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        tutorialPopup.SetActive(false);
        tutorialPopup.transform.SetAsLastSibling();
    }

    private void CacheMenuGroup()
    {
        Transform panel = transform.Find("Panel");
        if (panel == null)
        {
            panel = transform;
        }

        menuGroup = panel.GetComponent<CanvasGroup>();
        if (menuGroup == null)
        {
            menuGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ConfigureMenuNavigation()
    {
        UiNavigation.StyleButton(startButton);
        UiNavigation.StyleButton(quitButton);

        UiNavigation.SetExplicitNavigation(ageInput, null, consentToggle, null, hoursInput);
        UiNavigation.SetExplicitNavigation(hoursInput, null, consentToggle, ageInput, null);
        UiNavigation.SetExplicitNavigation(consentToggle, hoursInput, startButton, null, null);
        UiNavigation.SetExplicitNavigation(startButton, consentToggle, quitButton, null, null);
        UiNavigation.SetExplicitNavigation(quitButton, startButton, null, null, null);
    }

    private static void CreateControlColumn(Transform parent, string title, string body)
    {
        GameObject column = CreateUiObject(title, parent);
        column.AddComponent<Image>().color = new Color(0.94f, 0.94f, 0.94f, 1f);
        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreatePopupText(column.transform, title, 20f, FontStyles.Bold, Color.black, 28f);
        CreatePopupText(column.transform, body, 18f, FontStyles.Normal, new Color(0.15f, 0.15f, 0.15f), 140f);
    }

    private static TextMeshProUGUI CreatePopupText(
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        Color color,
        float height)
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
