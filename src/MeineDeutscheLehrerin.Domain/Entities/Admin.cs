namespace MeineDeutscheLehrerin.Domain.Entities;

/// <summary>Subscription tier. Paid unlocks the AI-cost features (within a daily quota).</summary>
public enum SubscriptionTier
{
    Free = 0,
    Paid = 1,
}

/// <summary>
/// A toggleable feature. Stored in the DB and flipped by an admin; the frontend reads the
/// public map to show/hide features, and the API enforces the sensitive ones server-side.
/// </summary>
public class FeatureFlag
{
    public string Key { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Description { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Well-known feature keys. Keep in sync with the seeded defaults.</summary>
public static class FeatureKeys
{
    public const string Vocabulary = "vocabulary";
    public const string Speaking = "speaking";
    public const string StudyPlan = "study_plan";
    public const string AiFeedback = "ai_feedback";
    public const string Registration = "registration";

    /// <summary>The default set seeded on first run (idempotent — existing flags are left alone).</summary>
    public static readonly IReadOnlyList<(string Key, string Description)> Defaults = new[]
    {
        (Vocabulary, "Vokabeltrainer (spaced-repetition vocabulary)"),
        (Speaking, "Sprechen exercises and the microphone recorder"),
        (StudyPlan, "The 2-week Lernplan"),
        (AiFeedback, "AI-graded Writing & Speaking feedback (falls back to the offline scorer when off)"),
        (Registration, "Allow new users to sign up"),
    };
}
