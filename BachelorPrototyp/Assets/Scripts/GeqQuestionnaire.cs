using System;
using UnityEngine;

[Serializable]
public class GeqItem
{
    public string itemKey;
    public string subscale;
    public string text;

    public GeqItem()
    {
    }

    public GeqItem(string itemKey, string subscale, string text)
    {
        this.itemKey = itemKey;
        this.subscale = subscale;
        this.text = text;
    }
}

[Serializable]
public class GeqItemAnswer
{
    public string itemKey;
    public string subscale;
    public string text;
    public int score;
}

[Serializable]
public class GeqSubscaleMeans
{
    public float flow;
    public float competence;
    public float positiveAffect;
    public float negativeAffect;
    public float tension;
    public float challenge;
}

[Serializable]
public class GeqQuestionnaireResult
{
    public string sessionId;
    public string condition;
    public string completedAtIso;
    public GeqItemAnswer[] items;
    public GeqSubscaleMeans subscaleMeans;
}

[Serializable]
public class GeqSessionExport
{
    public string sessionId;
    public bool consentGiven;
    public int age;
    public int weeklyPlayHours;
    public string[] playOrder;
    public GeqQuestionnaireResult[] results;
    public PostExperimentResult postExperiment;
}

public static class GeqCatalog
{
    public const int LikertMin = 0;
    public const int LikertMax = 4;

    public static readonly string[] LikertAnchors =
    {
        "0 - not at all",
        "1 - slightly",
        "2 - moderately",
        "3 - fairly",
        "4 - extremely"
    };

    // In-Game GEQ (IJsselsteijn et al.). Original items 1 and 4 (Sensory Immersion:
    // story / impressive) are excluded because they do not apply to this game.
    public static readonly GeqItem[] Items =
    {
        new GeqItem("igeq_02", "Competence", "I felt successful."),
        new GeqItem("igeq_03", "NegativeAffect", "I felt bored."),
        new GeqItem("igeq_05", "Flow", "I forgot everything around me."),
        new GeqItem("igeq_06", "Tension", "I felt frustrated."),
        new GeqItem("igeq_07", "NegativeAffect", "I found it tiresome."),
        new GeqItem("igeq_08", "Tension", "I felt irritable."),
        new GeqItem("igeq_09", "Competence", "I felt skilful."),
        new GeqItem("igeq_10", "Flow", "I felt completely absorbed."),
        new GeqItem("igeq_11", "PositiveAffect", "I felt content."),
        new GeqItem("igeq_12", "Challenge", "I felt challenged."),
        new GeqItem("igeq_13", "Challenge", "I had to put a lot of effort into it."),
        new GeqItem("igeq_14", "PositiveAffect", "I felt good.")
    };

    public static GeqSubscaleMeans ComputeMeans(GeqItemAnswer[] answers)
    {
        return new GeqSubscaleMeans
        {
            flow = Mean(answers, "Flow"),
            competence = Mean(answers, "Competence"),
            positiveAffect = Mean(answers, "PositiveAffect"),
            negativeAffect = Mean(answers, "NegativeAffect"),
            tension = Mean(answers, "Tension"),
            challenge = Mean(answers, "Challenge")
        };
    }

    private static float Mean(GeqItemAnswer[] answers, string subscale)
    {
        int sum = 0;
        int count = 0;

        for (int i = 0; i < answers.Length; i++)
        {
            if (answers[i].subscale != subscale)
            {
                continue;
            }

            sum += answers[i].score;
            count++;
        }

        return count == 0 ? 0f : (float)sum / count;
    }
}
