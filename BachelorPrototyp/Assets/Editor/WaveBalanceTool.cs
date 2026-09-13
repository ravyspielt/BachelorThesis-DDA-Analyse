using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WaveBalanceTool
{
    private const string ConditionScenePathFormat = "Assets/Scenes/{0}.unity";
    private const string WarmupSceneName = "Warmup";
    private static readonly string[] ConditionSceneNames = { "Explicit", "Implicit", "Control" };


    private const float StartHealthPerSecond = 1.95f;
    private const float EndHealthPerSecond = 2.35f;

    private const float RunDurationSeconds = 240f;
    private const float WarmupDurationSeconds = 120f;
    private const float TimeBetweenSubWaves = 2f;

    private const int WarmupSubWaveCount = 8;

    private struct SubWaveSpec
    {
        public readonly int[] Indices;
        public readonly float[] Weights;

        public SubWaveSpec(int[] indices, float[] weights)
        {
            Indices = indices;
            Weights = weights;
        }
    }

    private static readonly SubWaveSpec[][] RunSpec =
    {
        new[]
        {
            new SubWaveSpec(new[] { 0 }, new[] { 100f }),
            new SubWaveSpec(new[] { 0, 1 }, new[] { 80f, 20f })
        },
        new[]
        {
            new SubWaveSpec(new[] { 0, 1 }, new[] { 50f, 50f }),
            new SubWaveSpec(new[] { 0, 1, 2 }, new[] { 40f, 40f, 20f }),
            new SubWaveSpec(new[] { 0, 1, 2 }, new[] { 30f, 40f, 30f })
        },
        new[]
        {
            new SubWaveSpec(new[] { 0, 1, 2, 3 }, new[] { 25f, 35f, 30f, 10f }),
            new SubWaveSpec(new[] { 0, 1, 2, 3 }, new[] { 20f, 30f, 30f, 20f }),
            new SubWaveSpec(new[] { 0, 1, 2, 3 }, new[] { 15f, 25f, 35f, 25f })
        },
        new[]
        {
            new SubWaveSpec(new[] { 0, 1, 2, 3 }, new[] { 10f, 20f, 35f, 35f }),
            new SubWaveSpec(new[] { 1, 2, 3 }, new[] { 20f, 35f, 45f }),
            new SubWaveSpec(new[] { 2, 3 }, new[] { 40f, 60f })
        },
        new[]
        {
            new SubWaveSpec(new[] { 1, 2, 3 }, new[] { 15f, 30f, 55f }),
            new SubWaveSpec(new[] { 2, 3 }, new[] { 30f, 70f }),
            new SubWaveSpec(new[] { 2, 3 }, new[] { 20f, 80f }),
            new SubWaveSpec(new[] { 3 }, new[] { 100f })
        }
    };

    private static readonly SubWaveSpec WarmupSpec =
        new SubWaveSpec(new[] { 0, 1, 2, 3 }, new[] { 20f, 30f, 30f, 20f });

    [MenuItem("Bachelor/Wellen neu austarieren")]
    public static void RebalanceAllScenesFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        RebalanceAllScenes();
    }

    public static void RebalanceAllScenes()
    {
        StringBuilder report = new StringBuilder();

        for (int i = 0; i < ConditionSceneNames.Length; i++)
        {
            ApplyToScene(ConditionSceneNames[i], false, report);
        }

        ApplyToScene(WarmupSceneName, true, report);

        Debug.Log("[WaveBalance] Fertig.\n" + report);
    }

    private static void ApplyToScene(string sceneName, bool isWarmup, StringBuilder report)
    {
        string scenePath = string.Format(ConditionScenePathFormat, sceneName);
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();

        if (spawner == null)
        {
            Debug.LogError("[WaveBalance] Kein EnemySpawner in " + sceneName);
            return;
        }

        if (spawner.mainWaves == null || spawner.mainWaves.Length == 0)
        {
            Debug.LogError("[WaveBalance] Keine Wellen in " + sceneName);
            return;
        }

        GameObject[] enemyPrefabs = spawner.mainWaves[0].enemies;
        float[] healthPerIndex = ReadHealthPerIndex(enemyPrefabs, sceneName);

        if (healthPerIndex == null)
        {
            return;
        }

        spawner.mainWaves = isWarmup
            ? BuildWarmupWaves(enemyPrefabs, healthPerIndex, report)
            : BuildRunWaves(enemyPrefabs, healthPerIndex, report);

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static float[] ReadHealthPerIndex(GameObject[] enemyPrefabs, string sceneName)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError("[WaveBalance] Keine Gegner-Prefabs in " + sceneName);
            return null;
        }

        float[] health = new float[enemyPrefabs.Length];

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            EnemyBase enemy = enemyPrefabs[i] != null ? enemyPrefabs[i].GetComponent<EnemyBase>() : null;

            if (enemy == null)
            {
                Debug.LogError("[WaveBalance] Prefab " + i + " in " + sceneName + " hat kein EnemyBase.");
                return null;
            }

            health[i] = enemy.initHealth;
        }

        return health;
    }

    private static EnemyWave[] BuildRunWaves(GameObject[] enemyPrefabs, float[] healthPerIndex, StringBuilder report)
    {
        int subWaveCount = 0;

        for (int wave = 0; wave < RunSpec.Length; wave++)
        {
            subWaveCount += RunSpec[wave].Length;
        }

        float spawnBudget = RunDurationSeconds - subWaveCount * TimeBetweenSubWaves;
        float secondsPerSubWave = spawnBudget / subWaveCount;

        EnemyWave[] waves = new EnemyWave[RunSpec.Length];
        int flatIndex = 0;
        float totalSeconds = 0f;
        int totalEnemies = 0;

        for (int wave = 0; wave < RunSpec.Length; wave++)
        {
            SubWaveSpec[] specs = RunSpec[wave];
            SubWave[] subWaves = new SubWave[specs.Length];

            for (int sub = 0; sub < specs.Length; sub++)
            {
                float progress = subWaveCount > 1 ? flatIndex / (float)(subWaveCount - 1) : 0f;
                float targetHealthPerSecond = Mathf.Lerp(StartHealthPerSecond, EndHealthPerSecond, progress);

                subWaves[sub] = BuildSubWave(specs[sub], healthPerIndex, targetHealthPerSecond, secondsPerSubWave);

                totalSeconds += subWaves[sub].amount * subWaves[sub].spawnInterval + TimeBetweenSubWaves;
                totalEnemies += subWaves[sub].amount;
                AppendRow(report, "Runde", flatIndex, specs[sub], healthPerIndex, subWaves[sub]);
                flatIndex++;
            }

            waves[wave] = new EnemyWave
            {
                waveName = "Welle " + (wave + 1),
                enemies = enemyPrefabs,
                subWaves = subWaves,
                timeBetweenSubWaves = TimeBetweenSubWaves
            };
        }

        report.AppendLine($"Runde gesamt: {totalSeconds:F1}s, {totalEnemies} Gegner\n");
        return waves;
    }

    private static EnemyWave[] BuildWarmupWaves(GameObject[] enemyPrefabs, float[] healthPerIndex, StringBuilder report)
    {
        float targetHealthPerSecond = (StartHealthPerSecond + EndHealthPerSecond) * 0.5f;
        float spawnBudget = WarmupDurationSeconds - WarmupSubWaveCount * TimeBetweenSubWaves;
        float secondsPerSubWave = spawnBudget / WarmupSubWaveCount;

        SubWave[] subWaves = new SubWave[WarmupSubWaveCount];
        float totalSeconds = 0f;
        int totalEnemies = 0;

        for (int sub = 0; sub < WarmupSubWaveCount; sub++)
        {
            subWaves[sub] = BuildSubWave(WarmupSpec, healthPerIndex, targetHealthPerSecond, secondsPerSubWave);
            totalSeconds += subWaves[sub].amount * subWaves[sub].spawnInterval + TimeBetweenSubWaves;
            totalEnemies += subWaves[sub].amount;
            AppendRow(report, "Warmup", sub, WarmupSpec, healthPerIndex, subWaves[sub]);
        }

        report.AppendLine($"Warmup gesamt: {totalSeconds:F1}s, {totalEnemies} Gegner\n");

        return new[]
        {
            new EnemyWave
            {
                waveName = "Warmup Baseline",
                enemies = enemyPrefabs,
                subWaves = subWaves,
                timeBetweenSubWaves = TimeBetweenSubWaves
            }
        };
    }

    private static SubWave BuildSubWave(
        SubWaveSpec spec,
        float[] healthPerIndex,
        float targetHealthPerSecond,
        float secondsPerSubWave)
    {
        float averageHealth = AverageHealth(spec, healthPerIndex);
        float spawnInterval = averageHealth / targetHealthPerSecond;
        int amount = Mathf.Max(1, Mathf.RoundToInt(secondsPerSubWave / spawnInterval));

        return new SubWave
        {
            amount = amount,
            spawnInterval = (float)System.Math.Round(spawnInterval, 3),
            allowedEnemyIndices = (int[])spec.Indices.Clone(),
            spawnPercentages = (float[])spec.Weights.Clone()
        };
    }

    private static float AverageHealth(SubWaveSpec spec, float[] healthPerIndex)
    {
        float weightSum = 0f;
        float healthSum = 0f;

        for (int i = 0; i < spec.Indices.Length; i++)
        {
            int index = spec.Indices[i];

            if (index < 0 || index >= healthPerIndex.Length)
            {
                continue;
            }

            weightSum += spec.Weights[i];
            healthSum += spec.Weights[i] * healthPerIndex[index];
        }

        return weightSum > 0f ? healthSum / weightSum : 1f;
    }

    private static void AppendRow(
        StringBuilder report,
        string label,
        int index,
        SubWaveSpec spec,
        float[] healthPerIndex,
        SubWave subWave)
    {
        float averageHealth = AverageHealth(spec, healthPerIndex);
        float healthPerSecond = averageHealth / subWave.spawnInterval;

        report.AppendLine(
            $"{label} {index:00}: HP/Gegner={averageHealth:F2} Intervall={subWave.spawnInterval:F3}s " +
            $"Anzahl={subWave.amount} -> {healthPerSecond:F2} HP/s ueber {subWave.amount * subWave.spawnInterval:F1}s");
    }
}
