using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine.Networking;
#endif
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
#endif

public class ExperimentLevelStats
{
    public float durationSeconds;
    public int kills;
    public int deaths;
    public float killRate;
    public float baselineKillRate;
    public int spawnedEnemies;
    public float killShare;
    public float baselineKillShare;
    public float difficultyMultiplier;
    public int playIndex;
    public string[] playOrder;
    public int difficultyIncreaseCount;
    public int difficultyDecreaseCount;
    public int explicitNotificationCount;
}

public static class FirestoreExperimentStore
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private const string ProjectId = "";
    private const string ApiKey = "";
    private static FirestoreRequestRunner runner;
#endif

    private static bool initStarted;
    private static bool ready;

#if !(UNITY_WEBGL && !UNITY_EDITOR)
    private static readonly Queue<Action> pendingWrites = new Queue<Action>();
    private static FirebaseFirestore database;
#endif

    public static void Initialize()
    {
        if (initStarted)
        {
            return;
        }

        initStarted = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        ready = true;
        EnsureRunner();
        Debug.Log("[Firestore] WebGL REST-Modus bereit.");
#else
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("[Firestore] Dependency-Check fehlgeschlagen: " + task.Exception);
                return;
            }

            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning("[Firestore] Dependencies nicht verfuegbar: " + task.Result);
                return;
            }

            database = FirebaseFirestore.DefaultInstance;
            ready = true;
            Debug.Log("[Firestore] Bereit (natives SDK).");

            while (pendingWrites.Count > 0)
            {
                pendingWrites.Dequeue()?.Invoke();
            }
        });
#endif
    }

    public static void SaveSession(
        string userId,
        string[] playOrder = null,
        int age = 0,
        int weeklyPlayHours = 0)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "userId", userId ?? "" },
            { "age", age },
            { "weeklyPlayHours", weeklyPlayHours },
            { "consentGiven", true },
            { "consentAcceptedAtIso", DateTime.UtcNow.ToString("o") },
            { "createdAtIso", DateTime.UtcNow.ToString("o") },
            { "playOrder", ToStringList(playOrder) }
        };

        WriteDocument("users/" + userId, data);
    }

    public static void SaveLevel(string userId, string levelId, ExperimentLevelStats stats, GeqQuestionnaireResult geq)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "level", levelId },
            {
                "completedAtIso", geq != null && !string.IsNullOrEmpty(geq.completedAtIso)
                    ? geq.completedAtIso
                    : DateTime.UtcNow.ToString("o")
            },
            { "stats", ToStatsMap(stats) }
        };

        if (geq != null)
        {
            data["geq"] = ToGeqMap(geq);
        }

        WriteDocument("users/" + userId + "/levels/" + levelId, data);
    }

    public static void SavePostExperiment(string userId, PostExperimentResult result)
    {
        Dictionary<string, object> data = ToPostExperimentMap(result);
        data["userId"] = userId ?? "";
        WriteDocument("users/" + userId + "/levels/PostExperiment", data);
    }

    private static void WriteDocument(string documentPath, Dictionary<string, object> data)
    {
        Initialize();

#if UNITY_WEBGL && !UNITY_EDITOR
        EnsureRunner();
        runner.StartCoroutine(PatchDocumentRoutine(documentPath, data));
#else
        RunWhenReady(() =>
        {
            GetDocumentReference(documentPath).SetAsync(data)
                .ContinueWithOnMainThread(task => LogWriteResult(documentPath, task.Exception, null));
        });
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static IEnumerator PatchDocumentRoutine(string documentPath, Dictionary<string, object> data)
    {
        string url =
            "https://firestore.googleapis.com/v1/projects/" + ProjectId +
            "/databases/(default)/documents/" + documentPath +
            "?key=" + ApiKey;

        byte[] body = Encoding.UTF8.GetBytes(ToFirestoreDocumentJson(data));
        UnityWebRequest request = new UnityWebRequest(url, "PATCH");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        LogWriteResult(
            documentPath,
            success ? null : new Exception(request.error + " " + request.downloadHandler.text),
            request.downloadHandler != null ? request.downloadHandler.text : null);
    }

    private static FirestoreRequestRunner EnsureRunner()
    {
        if (runner != null)
        {
            return runner;
        }

        GameObject runnerObject = new GameObject("FirestoreRequestRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<FirestoreRequestRunner>();
        return runner;
    }
#else
    private static void RunWhenReady(Action action)
    {
        Initialize();

        if (ready)
        {
            action();
            return;
        }

        pendingWrites.Enqueue(action);
    }

    private static DocumentReference GetDocumentReference(string documentPath)
    {
        string[] parts = documentPath.Split('/');
        CollectionReference collection = database.Collection(parts[0]);
        DocumentReference document = collection.Document(parts[1]);

        for (int i = 2; i + 1 < parts.Length; i += 2)
        {
            document = document.Collection(parts[i]).Document(parts[i + 1]);
        }

        return document;
    }
#endif

    private static Dictionary<string, object> ToStatsMap(ExperimentLevelStats stats)
    {
        if (stats == null)
        {
            stats = new ExperimentLevelStats();
        }

        return new Dictionary<string, object>
        {
            { "durationSeconds", (double)stats.durationSeconds },
            { "kills", stats.kills },
            { "deaths", stats.deaths },
            { "killRate", (double)stats.killRate },
            { "baselineKillRate", (double)stats.baselineKillRate },
            { "spawnedEnemies", stats.spawnedEnemies },
            { "killShare", (double)stats.killShare },
            { "baselineKillShare", (double)stats.baselineKillShare },
            { "difficultyMultiplier", (double)stats.difficultyMultiplier },
            { "playIndex", stats.playIndex },
            { "playOrder", ToStringList(stats.playOrder) },
            { "difficultyIncreaseCount", stats.difficultyIncreaseCount },
            { "difficultyDecreaseCount", stats.difficultyDecreaseCount },
            { "explicitNotificationCount", stats.explicitNotificationCount }
        };
    }

    private static List<object> ToStringList(string[] values)
    {
        List<object> list = new List<object>();
        if (values == null)
        {
            return list;
        }

        for (int i = 0; i < values.Length; i++)
        {
            list.Add(values[i] ?? "");
        }

        return list;
    }

    private static Dictionary<string, object> ToGeqMap(GeqQuestionnaireResult result)
    {
        List<object> items = new List<object>();

        if (result.items != null)
        {
            for (int i = 0; i < result.items.Length; i++)
            {
                GeqItemAnswer item = result.items[i];
                items.Add(new Dictionary<string, object>
                {
                    { "itemKey", item.itemKey ?? "" },
                    { "subscale", item.subscale ?? "" },
                    { "text", item.text ?? "" },
                    { "score", item.score }
                });
            }
        }

        GeqSubscaleMeans means = result.subscaleMeans ?? new GeqSubscaleMeans();

        return new Dictionary<string, object>
        {
            { "items", items },
            {
                "subscaleMeans", new Dictionary<string, object>
                {
                    { "flow", (double)means.flow },
                    { "competence", (double)means.competence },
                    { "positiveAffect", (double)means.positiveAffect },
                    { "negativeAffect", (double)means.negativeAffect },
                    { "tension", (double)means.tension },
                    { "challenge", (double)means.challenge }
                }
            }
        };
    }

    private static Dictionary<string, object> ToPostExperimentMap(PostExperimentResult result)
    {
        if (result == null)
        {
            result = new PostExperimentResult();
        }

        List<object> answers = new List<object>();
        if (result.answers != null)
        {
            for (int i = 0; i < result.answers.Length; i++)
            {
                PostExperimentAnswer answer = result.answers[i];
                answers.Add(new Dictionary<string, object>
                {
                    { "itemKey", answer.itemKey ?? "" },
                    { "construct", answer.construct ?? "" },
                    { "text", answer.text ?? "" },
                    { "score", answer.score },
                    { "optionKey", answer.optionKey ?? "" },
                    { "optionText", answer.optionText ?? "" }
                });
            }
        }

        return new Dictionary<string, object>
        {
            { "completedAtIso", string.IsNullOrEmpty(result.completedAtIso) ? DateTime.UtcNow.ToString("o") : result.completedAtIso },
            { "playOrder", ToStringList(result.playOrder) },
            { "answers", answers }
        };
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static string ToFirestoreDocumentJson(Dictionary<string, object> data)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"fields\":");
        AppendFields(builder, data);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendFields(StringBuilder builder, Dictionary<string, object> data)
    {
        builder.Append('{');
        bool first = true;

        foreach (KeyValuePair<string, object> pair in data)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append('"').Append(EscapeJson(pair.Key)).Append("\":");
            AppendValue(builder, pair.Value);
        }

        builder.Append('}');
    }

    private static void AppendValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case null:
                builder.Append("{\"nullValue\":null}");
                break;
            case string text:
                builder.Append("{\"stringValue\":\"").Append(EscapeJson(text)).Append("\"}");
                break;
            case bool flag:
                builder.Append("{\"booleanValue\":").Append(flag ? "true" : "false").Append('}');
                break;
            case int number:
                builder.Append("{\"integerValue\":\"").Append(number).Append("\"}");
                break;
            case long longNumber:
                builder.Append("{\"integerValue\":\"").Append(longNumber).Append("\"}");
                break;
            case float floatNumber:
                builder.Append("{\"doubleValue\":").Append(floatNumber.ToString(CultureInfo.InvariantCulture)).Append('}');
                break;
            case double doubleNumber:
                builder.Append("{\"doubleValue\":").Append(doubleNumber.ToString(CultureInfo.InvariantCulture)).Append('}');
                break;
            case Dictionary<string, object> map:
                builder.Append("{\"mapValue\":{\"fields\":");
                AppendFields(builder, map);
                builder.Append("}}");
                break;
            case List<object> list:
                builder.Append("{\"arrayValue\":{\"values\":[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendValue(builder, list[i]);
                }

                builder.Append("]}}");
                break;
            default:
                builder.Append("{\"stringValue\":\"").Append(EscapeJson(value.ToString())).Append("\"}");
                break;
        }
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
#endif

    private static void LogWriteResult(string path, Exception exception, string responseBody)
    {
        if (exception != null)
        {
            string message = exception.ToString() + (string.IsNullOrEmpty(responseBody) ? "" : " " + responseBody);
            if (message.Contains("Missing or insufficient permissions") || message.Contains("PERMISSION_DENIED"))
            {
                Debug.LogError(
                    "[Firestore] Keine Schreibrechte fuer " + path + ". " +
                    "In der Firebase Console unter Firestore -> Regeln Schreibzugriff erlauben.");
                return;
            }

            Debug.LogError("[Firestore] Schreiben fehlgeschlagen (" + path + "): " + message);
            return;
        }

        Debug.Log("[Firestore] Gespeichert: " + path);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private sealed class FirestoreRequestRunner : MonoBehaviour
    {
    }
#endif
}
