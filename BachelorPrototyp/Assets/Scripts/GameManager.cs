using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameManager>();

                if (instance == null)
                {
                    GameObject gameManagerObject = new GameObject(nameof(GameManager));
                    instance = gameManagerObject.AddComponent<GameManager>();
                }
            }

            return instance;
        }
    }

    [Header("Experiment Configuration")]
    public DDAManager.DDAMode currentMode = DDAManager.DDAMode.Control;
    [SerializeField] private string startScreenSceneName = "StartScreen";
    [SerializeField] private string warmupSceneName = "Warmup";
    [SerializeField] private string[] versionSceneNames = { "Explicit", "Implicit", "Control" };
    [SerializeField] private float versionDuration = 240f;

    [Header("Warmup Tracking")]
    [SerializeField] private bool isWarmupActive = false;
    [SerializeField] private float warmupTimer = 0f;
    [SerializeField] private float warmupDuration = 120f;
    [SerializeField] private int warmupKillCount = 0;

    [Header("DDA State")]
    [SerializeField] private float baselineKillRate = 0f;
    [SerializeField] private float baselineKillShare = 0f;
    [SerializeField] private float difficultyMultiplier = 1.0f;
    [SerializeField] private float checkInterval = 5f;

    [Header("DDA Tuning")]
    [SerializeField] private float evaluationWindowSeconds = 20f;
    [SerializeField] private int minSpawnsForEvaluation = 8;
    [SerializeField] private float adjustmentStep = 0.15f;
    [SerializeField] private float thresholdDelta = 0.1f;
    [SerializeField] private float minMultiplier = 0.6f;
    [SerializeField] private float maxMultiplier = 1.8f;
    [SerializeField] private float minBaselineKillShare = 0.25f;
    [SerializeField] private float maxBaselineKillShare = 0.85f;

    private float currentRunKillRate = 0f;
    private float currentKillShare = 0f;
    private float sessionTimer = 0f;
    private int killCount = 0;
    private int deathCount = 0;
    private int difficultyIncreaseCount = 0;
    private int difficultyDecreaseCount = 0;
    private int explicitNotificationCount = 0;
    private float checkTimer = 0f;
    private bool hasBaseline = false;
    private bool sessionActive = false;
    private bool isTransitioning = false;
    private bool isQuestionnaireOpen = false;
    private bool isEndScreenOpen = false;
    private bool isPaused = false;
    private string sessionId;
    private int participantAge;
    private int participantWeeklyPlayHours;
    private GeqQuestionnaireUI geqUI;
    private PostExperimentQuestionnaireUI postExperimentUI;
    private PostExperimentResult postExperimentResult;
    private EndScreenUI endScreenUI;
    private VersionReadyPopup versionReadyPopup;
    private PauseOverlay pauseOverlay;
    private ExperimentLevelStats pendingLevelStats;
    private readonly List<GeqQuestionnaireResult> geqResults = new List<GeqQuestionnaireResult>();

    private readonly Queue<PerformanceSample> performanceSamples = new Queue<PerformanceSample>();
    private readonly List<float> warmupKillShares = new List<float>();
    private int runStartSpawnCount = 0;
    private Coroutine notificationCoroutine;

    private readonly List<string> versionSceneQueue = new List<string>();
    private int nextVersionIndex = 0;

    private struct PerformanceSample
    {
        public int kills;
        public int spawned;
    }

    private EnemySpawner enemySpawner;
    private GameObject explicitUIPanel;
    private TextMeshProUGUI notificationText;
    private TextMeshProUGUI countdownText;
    private const float CountdownAlpha = 0.9f;

    public float DifficultyMultiplier => difficultyMultiplier;
    public float BaselineKillRate => baselineKillRate;
    public float BaselineKillShare => baselineKillShare;
    public bool HasBaseline => hasBaseline;
    public bool IsQuestionnaireOpen => isQuestionnaireOpen;
    public bool IsPaused => isPaused;
    public bool BlocksGameplayInput => isQuestionnaireOpen || isEndScreenOpen || isTransitioning || isPaused;
    public bool ShouldHoldEnemySpawning => sessionActive && BlocksGameplayInput;
    public string SessionId => sessionId;
    public IReadOnlyList<GeqQuestionnaireResult> GeqResults => geqResults;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        FirestoreExperimentStore.Initialize();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        RefreshSceneBindings();
        checkTimer = checkInterval;
    }

    private void Update()
    {
        if (!sessionActive || isTransitioning || isPaused || isQuestionnaireOpen || isEndScreenOpen)
        {
            return;
        }

        if (isWarmupActive)
        {
            warmupTimer += Time.deltaTime;
            UpdateCountdownDisplay();

            if (warmupTimer >= warmupDuration)
            {
                FinishWarmup();
                return;
            }

            checkTimer -= Time.deltaTime;

            if (checkTimer <= 0f)
            {
                SampleWarmupPerformance();
                checkTimer = checkInterval;
            }

            return;
        }

        sessionTimer += Time.deltaTime;
        UpdateCountdownDisplay();

        if (sessionTimer >= versionDuration)
        {
            CompleteCurrentVersion();
            return;
        }

        checkTimer -= Time.deltaTime;

        if (checkTimer <= 0f)
        {
            EvaluateAndAdjustDifficulty();
            checkTimer = checkInterval;
        }
    }

    public void BeginSession(int age = 0, int weeklyPlayHours = 0)
    {
        sessionActive = true;
        isTransitioning = false;
        isQuestionnaireOpen = false;
        isEndScreenOpen = false;
        isPaused = false;
        nextVersionIndex = 0;
        hasBaseline = false;
        baselineKillRate = 0f;
        baselineKillShare = 0f;
        difficultyMultiplier = 1f;
        isWarmupActive = false;
        warmupTimer = 0f;
        warmupKillCount = 0;
        sessionTimer = 0f;
        killCount = 0;
        deathCount = 0;
        ResetDdaEventCounts();
        ResetPerformanceWindow();
        warmupKillShares.Clear();
        currentKillShare = 0f;
        runStartSpawnCount = 0;
        checkTimer = checkInterval;
        sessionId = System.Guid.NewGuid().ToString("N");
        participantAge = age;
        participantWeeklyPlayHours = weeklyPlayHours;
        pendingLevelStats = null;
        geqResults.Clear();
        postExperimentResult = null;
        Time.timeScale = 1f;
        if (pauseOverlay != null)
        {
            pauseOverlay.Hide();
        }

        if (versionReadyPopup != null)
        {
            versionReadyPopup.Hide();
        }

        BuildShuffledVersionQueue();
        Debug.Log(
            "[Experiment] Session " + sessionId +
            " Alter: " + participantAge +
            " Spielstunden/Woche: " + participantWeeklyPlayHours +
            " Reihenfolge: " + string.Join(" -> ", versionSceneQueue));
        PersistSessionExport();
        FirestoreExperimentStore.SaveSession(
            sessionId,
            versionSceneQueue.ToArray(),
            participantAge,
            participantWeeklyPlayHours);

        LoadExperimentScene(warmupSceneName);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        RefreshSceneBindings();
        UiNavigation.ConfigureEventSystem();

        if (!sessionActive || scene.name == startScreenSceneName)
        {
            isTransitioning = false;
            return;
        }

        if (currentMode == DDAManager.DDAMode.Warmup)
        {
            isTransitioning = false;
            StartWarmup();
            return;
        }

        ShowVersionReadyPopup();
    }

    public void ConfigureScene(DDAManager.DDAMode mode, EnemySpawner spawner, GameObject uiPanel, TextMeshProUGUI text)
    {
        StopNotification();

        currentMode = mode;
        enemySpawner = spawner;
        explicitUIPanel = uiPanel;
        notificationText = text;

        ApplyCurrentDifficultyToSpawner();

        if (explicitUIPanel != null && currentMode != DDAManager.DDAMode.Explicit)
        {
            explicitUIPanel.SetActive(false);
        }

        AlignExplicitNotification();
    }

    public void RegisterWarmupPerformance(int totalKills, float durationInMinutes)
    {
        baselineKillRate = totalKills / Mathf.Max(durationInMinutes, 0.01f);
        baselineKillShare = ComputeBaselineKillShare(totalKills);
        hasBaseline = true;
        Debug.Log(
            $"[Experiment] Warmup Baseline ermittelt: Anteil={baselineKillShare:F2} " +
            $"(wirksam {GetEffectiveBaselineKillShare():F2}), Kill-Rate={baselineKillRate:F2}/min");
    }

    private float ComputeBaselineKillShare(int totalKills)
    {
        if (warmupKillShares.Count > 0)
        {
            float sum = 0f;

            for (int i = 0; i < warmupKillShares.Count; i++)
            {
                sum += warmupKillShares[i];
            }

            return sum / warmupKillShares.Count;
        }

        int spawned = GetSpawnedCount();
        return spawned > 0 ? Mathf.Clamp01(totalKills / (float)spawned) : 0f;
    }

    public void StartWarmup()
    {
        currentMode = DDAManager.DDAMode.Warmup;
        isWarmupActive = true;
        warmupTimer = 0f;
        warmupKillCount = 0;
        sessionTimer = 0f;
        killCount = 0;
        deathCount = 0;
        ResetDdaEventCounts();
        ResetPerformanceWindow();
        warmupKillShares.Clear();
        currentKillShare = 0f;
        hasBaseline = false;
        checkTimer = checkInterval;
        difficultyMultiplier = 1f;
        ApplyCurrentDifficultyToSpawner();
        runStartSpawnCount = GetSpawnedCount();

        Debug.Log("[Experiment] Warmup gestartet. Erfasse Baseline...");
        UpdateCountdownDisplay();
    }

    public void FinishWarmup()
    {
        if (!isWarmupActive)
        {
            return;
        }

        isWarmupActive = false;

        float durationInMinutes = warmupDuration / 60f;
        RegisterWarmupPerformance(warmupKillCount, durationInMinutes);

        pendingLevelStats = CaptureStats(
            warmupDuration,
            warmupKillCount,
            warmupKillCount / Mathf.Max(durationInMinutes, 0.01f),
            baselineKillShare);
        FirestoreExperimentStore.SaveLevel(sessionId, DDAManager.DDAMode.Warmup.ToString(), pendingLevelStats, null);

        Debug.Log(
            $"[Experiment] Warmup beendet! Kills: {warmupKillCount}, Tode: {deathCount}, " +
            $"Gespawnt: {pendingLevelStats.spawnedEnemies}, Baseline-Anteil: {baselineKillShare:F2}");
        LoadNextVersionScene();
    }

    public void AddKill()
    {
        if (isWarmupActive)
        {
            warmupKillCount++;
            return;
        }

        killCount++;
    }

    public void AddDeath()
    {
        deathCount++;
    }

    private void BeginVersionRun()
    {
        isQuestionnaireOpen = false;
        Time.timeScale = 1f;
        isWarmupActive = false;
        sessionTimer = 0f;
        killCount = 0;
        deathCount = 0;
        ResetDdaEventCounts();
        ResetPerformanceWindow();
        currentKillShare = 0f;
        checkTimer = checkInterval;
        difficultyMultiplier = 1f;
        ApplyCurrentDifficultyToSpawner();
        runStartSpawnCount = GetSpawnedCount();

        Debug.Log($"[Experiment] Starte {currentMode}, Dauer: {versionDuration}s, Baseline-Anteil: {GetEffectiveBaselineKillShare():F2}, Multiplikator: {difficultyMultiplier:F2}");
        UpdateCountdownDisplay();
    }

    private void CompleteCurrentVersion()
    {
        float durationInMinutes = Mathf.Max(sessionTimer / 60f, 0.01f);
        currentRunKillRate = killCount / durationInMinutes;

        int spawnedInRun = Mathf.Max(0, GetSpawnedCount() - runStartSpawnCount);
        float runKillShare = spawnedInRun > 0 ? Mathf.Clamp01(killCount / (float)spawnedInRun) : 0f;

        Debug.Log($"[Experiment] {currentMode} beendet. t={sessionTimer:F1}s Kills: {killCount}, Tode: {deathCount}, Gespawnt: {spawnedInRun}, Anteil: {runKillShare:F2}, Baseline-Anteil: {GetEffectiveBaselineKillShare():F2}, Multiplikator: {difficultyMultiplier:F2}, +{difficultyIncreaseCount}/-{difficultyDecreaseCount}, Meldungen: {explicitNotificationCount}");

        pendingLevelStats = CaptureStats(sessionTimer, killCount, currentRunKillRate, runKillShare);
        ClearPause();
        isTransitioning = true;
        ShowGeqQuestionnaire();
    }

    private void ShowGeqQuestionnaire()
    {
        isQuestionnaireOpen = true;
        Time.timeScale = 0f;

        if (geqUI == null)
        {
            GameObject uiObject = new GameObject(nameof(GeqQuestionnaireUI));
            uiObject.transform.SetParent(transform, false);
            geqUI = uiObject.AddComponent<GeqQuestionnaireUI>();
        }

        geqUI.Show(sessionId, currentMode.ToString(), OnGeqCompleted);
    }

    private void ShowVersionReadyPopup()
    {
        isTransitioning = true;
        Time.timeScale = 0f;

        if (versionReadyPopup == null)
        {
            GameObject uiObject = new GameObject(nameof(VersionReadyPopup));
            uiObject.transform.SetParent(transform, false);
            versionReadyPopup = uiObject.AddComponent<VersionReadyPopup>();
        }

        versionReadyPopup.Show(OnVersionReadyConfirmed);
    }

    private void OnVersionReadyConfirmed()
    {
        isTransitioning = false;
        Time.timeScale = 1f;
        BeginVersionRun();
    }

    public void TogglePause()
    {
        if (!sessionActive || isQuestionnaireOpen || isEndScreenOpen || isTransitioning)
        {
            return;
        }

        SetPaused(!isPaused);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (paused)
        {
            if (pauseOverlay == null)
            {
                GameObject uiObject = new GameObject(nameof(PauseOverlay));
                uiObject.transform.SetParent(transform, false);
                pauseOverlay = uiObject.AddComponent<PauseOverlay>();
            }

            pauseOverlay.Show();
        }
        else if (pauseOverlay != null)
        {
            pauseOverlay.Hide();
        }
    }

    private void ClearPause()
    {
        isPaused = false;
        if (pauseOverlay != null)
        {
            pauseOverlay.Hide();
        }
    }

    private void OnGeqCompleted(GeqQuestionnaireResult result)
    {
        geqResults.Add(result);
        PersistGeqResult(result);
        FirestoreExperimentStore.SaveLevel(sessionId, currentMode.ToString(), pendingLevelStats, result);
        Debug.Log("[GEQ] " + currentMode + "\n" + JsonUtility.ToJson(result, true));

        isQuestionnaireOpen = false;

        if (nextVersionIndex >= versionSceneQueue.Count)
        {
            ShowPostExperimentQuestionnaire();
            return;
        }

        Time.timeScale = 1f;
        LoadNextVersionScene();
    }

    private void ShowPostExperimentQuestionnaire()
    {
        isQuestionnaireOpen = true;
        Time.timeScale = 0f;

        if (geqUI != null)
        {
            geqUI.Hide();
        }

        if (postExperimentUI == null)
        {
            GameObject uiObject = new GameObject(nameof(PostExperimentQuestionnaireUI));
            uiObject.transform.SetParent(transform, false);
            postExperimentUI = uiObject.AddComponent<PostExperimentQuestionnaireUI>();
        }

        postExperimentUI.Show(sessionId, versionSceneQueue.ToArray(), OnPostExperimentCompleted);
    }

    private void OnPostExperimentCompleted(PostExperimentResult result)
    {
        postExperimentResult = result;
        PersistPostExperimentResult(result);
        FirestoreExperimentStore.SavePostExperiment(sessionId, result);
        Debug.Log("[PostExperiment] " + JsonUtility.ToJson(result, true));

        isQuestionnaireOpen = false;
        EndSession();
    }

    private void PersistPostExperimentResult(PostExperimentResult result)
    {
        try
        {
            string directory = GetGeqDirectory();
            string resultPath = Path.Combine(directory, result.sessionId + "_post.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            PersistSessionExport();
            Debug.Log("[PostExperiment] Gespeichert: " + resultPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[PostExperiment] Lokales Speichern fehlgeschlagen: " + exception.Message);
        }
    }

    private void PersistGeqResult(GeqQuestionnaireResult result)
    {
        try
        {
            string directory = GetGeqDirectory();
            string resultPath = Path.Combine(directory, result.sessionId + "_" + result.condition + ".json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            PersistSessionExport();
            Debug.Log("[GEQ] Gespeichert: " + resultPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GEQ] Lokales Speichern fehlgeschlagen: " + exception.Message);
        }
    }

    private void PersistSessionExport()
    {
        try
        {
            string directory = GetGeqDirectory();
            GeqSessionExport export = new GeqSessionExport
            {
                sessionId = sessionId,
                consentGiven = true,
                age = participantAge,
                weeklyPlayHours = participantWeeklyPlayHours,
                playOrder = versionSceneQueue.ToArray(),
                results = geqResults.ToArray(),
                postExperiment = postExperimentResult
            };
            string sessionPath = Path.Combine(directory, "session_" + sessionId + ".json");
            File.WriteAllText(sessionPath, JsonUtility.ToJson(export, true));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GEQ] Session-Export fehlgeschlagen: " + exception.Message);
        }
    }

    private static string GetGeqDirectory()
    {
        string directory = Path.Combine(Application.persistentDataPath, "geq");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private ExperimentLevelStats CaptureStats(float durationSeconds, int kills, float killRate, float killShare)
    {
        return new ExperimentLevelStats
        {
            durationSeconds = durationSeconds,
            kills = kills,
            deaths = deathCount,
            killRate = killRate,
            baselineKillRate = baselineKillRate,
            spawnedEnemies = Mathf.Max(0, GetSpawnedCount() - runStartSpawnCount),
            killShare = killShare,
            baselineKillShare = baselineKillShare,
            difficultyMultiplier = difficultyMultiplier,
            playIndex = GetPlayIndex(),
            playOrder = versionSceneQueue.ToArray(),
            difficultyIncreaseCount = difficultyIncreaseCount,
            difficultyDecreaseCount = difficultyDecreaseCount,
            explicitNotificationCount = explicitNotificationCount
        };
    }

    private int GetPlayIndex()
    {
        if (currentMode == DDAManager.DDAMode.Warmup)
        {
            return -1;
        }

        return Mathf.Max(0, nextVersionIndex - 1);
    }

    private void ResetDdaEventCounts()
    {
        difficultyIncreaseCount = 0;
        difficultyDecreaseCount = 0;
        explicitNotificationCount = 0;
    }

    private void LoadNextVersionScene()
    {
        if (nextVersionIndex >= versionSceneQueue.Count)
        {
            EndSession();
            return;
        }

        string nextSceneName = versionSceneQueue[nextVersionIndex];
        nextVersionIndex++;
        LoadExperimentScene(nextSceneName);
    }

    private void EndSession()
    {
        sessionActive = false;
        isTransitioning = true;
        isQuestionnaireOpen = false;
        isEndScreenOpen = true;
        ClearPause();
        Time.timeScale = 0f;

        if (geqUI != null)
        {
            geqUI.Hide();
        }

        if (postExperimentUI != null)
        {
            postExperimentUI.Hide();
        }

        Debug.Log("[Experiment] Session beendet. Zeige Endscreen.");
        ShowEndScreen();
    }

    private void ShowEndScreen()
    {
        if (endScreenUI == null)
        {
            GameObject uiObject = new GameObject(nameof(EndScreenUI));
            uiObject.transform.SetParent(transform, false);
            endScreenUI = uiObject.AddComponent<EndScreenUI>();
        }

        endScreenUI.Show(QuitApplication);
    }

    private void QuitApplication()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void LoadExperimentScene(string sceneName)
    {
        isTransitioning = true;
        SceneManager.LoadScene(sceneName);
    }

    private void BuildShuffledVersionQueue()
    {
        versionSceneQueue.Clear();
        versionSceneQueue.AddRange(versionSceneNames);

        for (int i = versionSceneQueue.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            string current = versionSceneQueue[i];
            versionSceneQueue[i] = versionSceneQueue[swapIndex];
            versionSceneQueue[swapIndex] = current;
        }
    }

    private void RefreshSceneBindings()
    {
        EnemySpawner foundSpawner = FindAnyObjectByType<EnemySpawner>();
        DDAManager ddaManager = FindAnyObjectByType<DDAManager>();

        if (ddaManager != null)
        {
            ConfigureScene(ddaManager.currentMode, foundSpawner, ddaManager.explicitUIPanel, ddaManager.notificationText);
        }
        else
        {
            StopNotification();
            enemySpawner = foundSpawner;
            ApplyCurrentDifficultyToSpawner();
        }

        BindCountdownText();
        UpdateCountdownDisplay();
    }

    private void AlignExplicitNotification()
    {
        if (currentMode != DDAManager.DDAMode.Explicit || explicitUIPanel == null)
        {
            return;
        }

        RectTransform panelRect = explicitUIPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(24f, 24f);
            panelRect.sizeDelta = new Vector2(520f, 72f);
        }

        if (notificationText != null)
        {
            notificationText.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    private void BindCountdownText()
    {
        countdownText = null;
        TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "CountdownText")
            {
                countdownText = texts[i];
                ApplyCountdownTransparency();
                return;
            }
        }
    }

    private void UpdateCountdownDisplay()
    {
        if (countdownText == null)
        {
            return;
        }

        float remaining = isWarmupActive
            ? warmupDuration - warmupTimer
            : versionDuration - sessionTimer;

        remaining = Mathf.Max(0f, remaining);
        int totalSeconds = Mathf.CeilToInt(remaining);
        countdownText.text = $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        ApplyCountdownTransparency();
    }

    private void ApplyCountdownTransparency()
    {
        if (countdownText == null)
        {
            return;
        }

        countdownText.color = new Color(0f, 0f, 0f, CountdownAlpha);
        countdownText.alpha = CountdownAlpha;
    }

    private int GetSpawnedCount()
    {
        return enemySpawner != null ? enemySpawner.TotalSpawned : 0;
    }

    private int GetRequiredSampleCount()
    {
        float interval = Mathf.Max(checkInterval, 0.01f);
        return Mathf.Max(2, Mathf.CeilToInt(evaluationWindowSeconds / interval) + 1);
    }

    private void ResetPerformanceWindow()
    {
        performanceSamples.Clear();
    }

    private bool TryComputeKillShare(int kills, out float killShare)
    {
        killShare = 0f;

        int spawnedNow = GetSpawnedCount();
        performanceSamples.Enqueue(new PerformanceSample { kills = kills, spawned = spawnedNow });

        int requiredSamples = GetRequiredSampleCount();

        while (performanceSamples.Count > requiredSamples)
        {
            performanceSamples.Dequeue();
        }

        if (performanceSamples.Count < requiredSamples)
        {
            return false;
        }

        PerformanceSample windowStart = performanceSamples.Peek();
        int spawnedInWindow = spawnedNow - windowStart.spawned;

        if (spawnedInWindow < minSpawnsForEvaluation)
        {
            return false;
        }

        killShare = Mathf.Clamp01((kills - windowStart.kills) / (float)spawnedInWindow);
        return true;
    }

    private void SampleWarmupPerformance()
    {
        if (TryComputeKillShare(warmupKillCount, out float killShare))
        {
            warmupKillShares.Add(killShare);
            currentKillShare = killShare;
        }

        Debug.Log(
            $"[DDA] Warmup t={warmupTimer:F1}s kills={warmupKillCount} spawned={GetSpawnedCount()} " +
            $"share={currentKillShare:F2} samples={warmupKillShares.Count}");
    }

    private void EvaluateAndAdjustDifficulty()
    {
        float currentMinutes = sessionTimer / 60f;
        currentRunKillRate = killCount / Mathf.Max(currentMinutes, 0.01f);

        bool hasKillShare = TryComputeKillShare(killCount, out float killShare);

        if (hasKillShare)
        {
            currentKillShare = killShare;
        }

        string decision = "keine Anpassung";

        bool ddaActive = hasBaseline
            && currentMode != DDAManager.DDAMode.Control
            && currentMode != DDAManager.DDAMode.Warmup;

        if (currentMode == DDAManager.DDAMode.Control)
        {
            decision = "DDA inaktiv (Control)";
        }
        else if (!hasBaseline)
        {
            decision = "keine Baseline";
        }
        else if (!hasKillShare)
        {
            decision = "Fenster unvollstaendig";
        }
        else if (ddaActive)
        {
            float baseline = GetEffectiveBaselineKillShare();

            if (killShare > baseline + thresholdDelta)
            {
                AdjustDifficulty(adjustmentStep, "Difficulty increased!");
                decision = "erhoeht";
            }
            else if (killShare < baseline - thresholdDelta)
            {
                AdjustDifficulty(-adjustmentStep, "Difficulty decreased!");
                decision = "verringert";
            }
        }

        LogDdaMetrics(decision);
    }

    private float GetEffectiveBaselineKillShare()
    {
        return Mathf.Clamp(baselineKillShare, minBaselineKillShare, maxBaselineKillShare);
    }

    private void LogDdaMetrics(string decision)
    {
        float baseline = GetEffectiveBaselineKillShare();

        Debug.Log(
            $"[DDA] Mode={currentMode} t={sessionTimer:F1}s kills={killCount} deaths={deathCount} " +
            $"spawned={GetSpawnedCount()} share={currentKillShare:F2} baseline={baseline:F2} " +
            $"low={baseline - thresholdDelta:F2} high={baseline + thresholdDelta:F2} " +
            $"killRate={currentRunKillRate:F2}/min multiplier={difficultyMultiplier:F2} decision={decision} " +
            $"up={difficultyIncreaseCount} down={difficultyDecreaseCount} notices={explicitNotificationCount}");
    }

    private void AdjustDifficulty(float amount, string message)
    {
        float previousMultiplier = difficultyMultiplier;
        difficultyMultiplier = Mathf.Clamp(difficultyMultiplier + amount, minMultiplier, maxMultiplier);

        if (difficultyMultiplier > previousMultiplier)
        {
            difficultyIncreaseCount++;
        }
        else if (difficultyMultiplier < previousMultiplier)
        {
            difficultyDecreaseCount++;
        }
        else
        {
            return;
        }

        ResetPerformanceWindow();
        ApplyCurrentDifficultyToSpawner();

        if (currentMode == DDAManager.DDAMode.Explicit && explicitUIPanel != null)
        {
            explicitNotificationCount++;
            StopNotification();
            notificationCoroutine = StartCoroutine(ShowNotification(message));
        }
    }

    private void StopNotification()
    {
        if (notificationCoroutine == null)
        {
            return;
        }

        StopCoroutine(notificationCoroutine);
        notificationCoroutine = null;

        if (explicitUIPanel != null)
        {
            explicitUIPanel.SetActive(false);
        }
    }

    private void ApplyCurrentDifficultyToSpawner()
    {
        if (enemySpawner != null)
        {
            enemySpawner.ApplyDDAMultiplier(difficultyMultiplier);
        }
    }

    private System.Collections.IEnumerator ShowNotification(string msg)
    {
        if (notificationText != null)
        {
            notificationText.text = msg;
        }

        explicitUIPanel.SetActive(true);
        yield return new WaitForSeconds(2f);
        explicitUIPanel.SetActive(false);
        notificationCoroutine = null;
    }
}
