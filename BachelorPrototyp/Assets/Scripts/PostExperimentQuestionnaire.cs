using System;

public enum PostQuestionType
{
    Likert,
    Choice
}

[Serializable]
public class PostExperimentOption
{
    public string optionKey;
    public string text;

    public PostExperimentOption()
    {
    }

    public PostExperimentOption(string optionKey, string text)
    {
        this.optionKey = optionKey;
        this.text = text;
    }
}

[Serializable]
public class PostExperimentItem
{
    public string itemKey;
    public string construct;
    public string text;
    public PostExperimentOption[] options;

    public PostQuestionType type =>
        options != null && options.Length > 0 ? PostQuestionType.Choice : PostQuestionType.Likert;

    public PostExperimentItem()
    {
    }

    public PostExperimentItem(
        string itemKey,
        string construct,
        string text,
        PostExperimentOption[] options = null)
    {
        this.itemKey = itemKey;
        this.construct = construct;
        this.text = text;
        this.options = options;
    }
}

[Serializable]
public class PostExperimentAnswer
{
    public string itemKey;
    public string construct;
    public string text;
    public int score;
    public string optionKey;
    public string optionText;
}

[Serializable]
public class PostExperimentResult
{
    public string sessionId;
    public string completedAtIso;
    public string[] playOrder;
    public PostExperimentAnswer[] answers;
}

public static class PostExperimentCatalog
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

    public static readonly PostExperimentItem[] Items =
    {
        new PostExperimentItem(
            "noticed_difficulty_change",
            "ManipulationCheck",
            "I noticed the difficulty changing during play, even when no message told me."),
        new PostExperimentItem(
            "saw_difficulty_messages",
            "ManipulationCheck",
            "I saw on-screen messages about difficulty changes."),
        new PostExperimentItem(
            "behaviour_changed_performance",
            "PerceivedAdaptation",
            "Even without any message, the game behaved differently when I was doing very well or very badly."),
        new PostExperimentItem(
            "challenge_matched_skill",
            "DifficultyAppropriateness",
            "The game matched the challenge to my skills and abilities as a player."),
        new PostExperimentItem(
            "right_amount_of_challenge",
            "DifficultyAppropriateness",
            "I felt just the right amount of challenge."),
        new PostExperimentItem(
            "felt_in_control",
            "Agency",
            "I felt that I had everything under control."),
        new PostExperimentItem(
            "dda_attractiveness",
            "DdaAttractiveness",
            "I would like games like this to adapt difficulty to my skill."),
        new PostExperimentItem(
            "transparency_preference",
            "TransparencyPreference",
            "If a game adapts its difficulty to your skill, how would you prefer it to work?",
            new[]
            {
                new PostExperimentOption("tell_me", "Tell me when the difficulty changes"),
                new PostExperimentOption("quiet", "Adapt quietly, without telling me"),
                new PostExperimentOption("no_adapt", "Do not adapt the difficulty"),
                new PostExperimentOption("no_preference", "No preference")
            }),
        new PostExperimentItem(
            "preferred_round",
            "RoundPreference",
            "Which of the three main rounds felt best to you? (not the warm-up)",
            new[]
            {
                new PostExperimentOption("first", "The first main round"),
                new PostExperimentOption("second", "The second main round"),
                new PostExperimentOption("third", "The third main round"),
                new PostExperimentOption("unsure", "Not sure / no preference")
            })
    };
}
