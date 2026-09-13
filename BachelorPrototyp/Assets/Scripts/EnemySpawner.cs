using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    [Header("Wave Settings")]
    public EnemyWave[] mainWaves;

    [Header("Spawn Settings")]
    [Tooltip("Abstand außerhalb des Kamera-Bildschirms, in dem Gegner spawnen.")]
    public float spawnOffset = 1.5f;
    [Tooltip("Wenn die letzte Welle durch ist, wird sie wiederholt statt von vorne zu beginnen.")]
    public bool loopWaves = true;

    private int currentMainWaveIndex = 0;
    private bool isSpawning = false;

    [Header("DDA Integration")]
    [Tooltip("Wie stark der Schwierigkeits-Multiplikator auf die Spawn-Rate durchschlaegt. 1 = voll, 0 = gar nicht.")]
    [SerializeField] private float spawnScaleInfluence = 0.5f;

    private float currentDifficultyMultiplier = 1.0f;

    public int TotalSpawned { get; private set; }

    void Start()
    {
        if (mainWaves.Length > 0)
        {
            StartCoroutine(RunMainWave(currentMainWaveIndex));
        }
    }

    IEnumerator RunMainWave(int waveIndex)
    {
        if (waveIndex >= mainWaves.Length)
        {
            Debug.Log("Alle Hauptwellen abgeschlossen!");
            yield break;
        }

        while (GameManager.Instance != null && GameManager.Instance.ShouldHoldEnemySpawning)
        {
            yield return null;
        }

        currentMainWaveIndex = waveIndex;
        EnemyWave currentMainWave = mainWaves[currentMainWaveIndex];
        Debug.Log("Starte Hauptwelle: " + currentMainWave.waveName);

        isSpawning = true;

        foreach (SubWave subWave in currentMainWave.subWaves)
        {
            Debug.Log("Starte Unter-Welle mit " + subWave.amount + " Gegnern.");

            for (int i = 0; i < subWave.amount; i++)
            {
                SpawnEnemy(currentMainWave.enemies, subWave);
                yield return new WaitForSeconds(CurrentSpawnInterval(subWave.spawnInterval));
            }

            yield return new WaitForSeconds(currentMainWave.timeBetweenSubWaves);
        }

        isSpawning = false;
        Debug.Log("Hauptwelle abgeschlossen: " + currentMainWave.waveName);

        if (loopWaves && mainWaves.Length > 0)
        {
            int nextWaveIndex = Mathf.Min(waveIndex + 1, mainWaves.Length - 1);
            StartCoroutine(RunMainWave(nextWaveIndex));
        }
    }

    void SpawnEnemy(GameObject[] mainWaveEnemies, SubWave subWave)
    {
        GameObject enemyPrefab = subWave.GetRandomEnemyFromSubWave(mainWaveEnemies);

        if (enemyPrefab != null)
        {
            Vector3 spawnPos = GetRandomSpawnPositionOutsideCamera();
            GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            EnemyBase enemy = spawnedEnemy.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.ApplyDifficultyScaling(currentDifficultyMultiplier);
            }

            TotalSpawned++;
        }
    }

    Vector3 GetRandomSpawnPositionOutsideCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        int side = Random.Range(0, 4);
        Vector3 spawnViewportPos = Vector3.zero;

        switch (side)
        {
            case 0:
                spawnViewportPos = new Vector3(Random.Range(0f, 1f), 1f + (spawnOffset / cam.scaledPixelHeight), 10f);
                break;
            case 1:
                spawnViewportPos = new Vector3(Random.Range(0f, 1f), 0f - (spawnOffset / cam.scaledPixelHeight), 10f);
                break;
            case 2:
                spawnViewportPos = new Vector3(0f - (spawnOffset / cam.scaledPixelWidth), Random.Range(0f, 1f), 10f);
                break;
            case 3:
                spawnViewportPos = new Vector3(1f + (spawnOffset / cam.scaledPixelWidth), Random.Range(0f, 1f), 10f);
                break;
        }

        spawnViewportPos.z = cam.nearClipPlane + 5f;
        return cam.ViewportToWorldPoint(spawnViewportPos);
    }

    public void ApplyDDAMultiplier(float multiplier)
    {
        currentDifficultyMultiplier = multiplier;
    }

    private float CurrentSpawnInterval(float baseInterval)
    {
        float rateScale = 1f + (currentDifficultyMultiplier - 1f) * spawnScaleInfluence;
        return baseInterval / Mathf.Max(rateScale, 0.25f);
    }
}

[System.Serializable]
public class EnemyWave
{
    [Header("Main Wave Configuration")]
    public string waveName = "Welle 1";

    [Tooltip("Der globale Pool an Gegnertypen für diese Hauptwelle (z.B. Index 0 = Zombie, Index 1 = Skelett, etc.).")]
    public GameObject[] enemies;

    [Tooltip("Die einzelnen Unter-Wellen (Spawn-Wellen) innerhalb dieser Hauptwelle.")]
    public SubWave[] subWaves;

    [Tooltip("Pause zwischen den einzelnen Unter-Wellen in Sekunden.")]
    public float timeBetweenSubWaves = 2f;
}

[System.Serializable]
public class SubWave
{
    [Header("Sub-Wave Configuration")]
    [Tooltip("Wie viele Gegner insgesamt in dieser Unter-Welle gespawnt werden sollen.")]
    public int amount = 10;

    [Tooltip("Zeitabstand zwischen den Spawns in dieser Unter-Welle.")]
    public float spawnInterval = 1f;

    [Header("Enemy Selection")]
    [Tooltip("Die Indizes der Gegner aus der Haupt-Liste (z.B. 0 und 1), die in dieser Subwave genutzt werden dürfen.")]
    public int[] allowedEnemyIndices;

    [Tooltip("Die prozentualen Gewichte für die oben genannten Indizes (muss gleich lang sein wie allowedEnemyIndices).")]
    public float[] spawnPercentages;

    public GameObject GetRandomEnemyFromSubWave(GameObject[] mainWaveEnemies)
    {
        if (mainWaveEnemies == null || mainWaveEnemies.Length == 0) return null;

        if (allowedEnemyIndices == null || allowedEnemyIndices.Length == 0)
        {
            return mainWaveEnemies[Random.Range(0, mainWaveEnemies.Length)];
        }

        if (spawnPercentages == null || spawnPercentages.Length != allowedEnemyIndices.Length)
        {
            int randomIdx = allowedEnemyIndices[Random.Range(0, allowedEnemyIndices.Length)];
            if (randomIdx >= 0 && randomIdx < mainWaveEnemies.Length)
                return mainWaveEnemies[randomIdx];
            return null;
        }

        float totalWeight = 0f;
        foreach (float weight in spawnPercentages)
        {
            totalWeight += weight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        for (int i = 0; i < allowedEnemyIndices.Length; i++)
        {
            currentSum += spawnPercentages[i];
            if (randomValue <= currentSum)
            {
                int enemyIndex = allowedEnemyIndices[i];
                if (enemyIndex >= 0 && enemyIndex < mainWaveEnemies.Length)
                {
                    return mainWaveEnemies[enemyIndex];
                }
            }
        }

        int fallbackIndex = allowedEnemyIndices[0];
        return (fallbackIndex >= 0 && fallbackIndex < mainWaveEnemies.Length) ? mainWaveEnemies[fallbackIndex] : null;
    }

}
