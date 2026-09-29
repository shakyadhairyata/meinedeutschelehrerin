namespace MeineDeutscheLehrerin.Domain.Entities;

/// <summary>
/// A cached dictionary lookup, keyed by the word's normalized surface form (so even inflected
/// forms are a free hit next time). Kept separate from <see cref="VocabularyItem"/> on purpose:
/// looked-up words must NOT enter the study/SRS deck, but repeat lookups should still be free.
/// </summary>
public class WordLookupEntry
{
    public int Id { get; set; }

    /// <summary>Normalized surface form the learner hovered (letters only, lower-cased). Unique key.</summary>
    public string Word { get; set; } = "";

    /// <summary>Base form / lemma (with article for nouns, e.g. "der Hund").</summary>
    public string German { get; set; } = "";
    public string English { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    public string? Article { get; set; }
    public string? Plural { get; set; }
    public string Example { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
