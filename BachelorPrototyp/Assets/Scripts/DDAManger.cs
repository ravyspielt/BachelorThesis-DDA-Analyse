using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DDAManager : MonoBehaviour
{
    public enum DDAMode { Warmup, Explicit, Implicit, Control }
    
    [Header("Experiment Configuration")]
    public DDAMode currentMode = DDAMode.Control;

    [Header("References")]
    public EnemySpawner enemySpawner;
    public GameObject explicitUIPanel;
    public TextMeshProUGUI notificationText;

    private void Awake()
    {
        SyncWithGameManager();
    }

    private void OnEnable()
    {
        SyncWithGameManager();
    }

    private void Start()
    {
        if (explicitUIPanel != null)
        {
            explicitUIPanel.SetActive(false);
        }
    }

    public void RegisterWarmupPerformance(int totalKills, float durationInMinutes)
    {
        GameManager.Instance.RegisterWarmupPerformance(totalKills, durationInMinutes);
    }

    public void AddKill()
    {
        GameManager.Instance.AddKill();
    }

    private void SyncWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ConfigureScene(currentMode, enemySpawner, explicitUIPanel, notificationText);
        }
    }
}
