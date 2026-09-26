using System.Linq;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface ILookupService
{
    /// <summary>Fast, free, offline: match a word against the existing vocabulary. Null on a miss.</summary>
    Task<WordLookupDto?> MatchVocabAsync(string word, CancellationToken ct = default);

    /// <summary>Miss path: gloss the word via the language-service (AI) and SAVE the base form to the
    /// vocabulary, so it is a free vocab hit next time. Null if the word can't be resolved.</summary>
    Task<WordLookupDto?> GlossAndSaveAsync(string word, string? context, CefrLevel level, CancellationToken ct = default);
}

public class LookupService : ILookupService
{
    private readonly AppDbContext _db;
    private readonly ILanguageService _lang;

    public LookupService(AppDbContext db, ILanguageService lang)
    {
        _db = db;
        _lang = lang;
    }

    /// <summary>Letters only (keeps German äöüß), lower-cased — strips surrounding punctuation.</summary>
    private static string Normalize(string w) =>
        new string((w ?? "").Trim().Where(c => char.IsLetter(c) || c == '-').ToArray()).ToLowerInvariant();

    public async Task<WordLookupDto?> MatchVocabAsync(string word, CancellationToken ct = default)
    {
        var norm = Normalize(word);
        if (norm.Length == 0) return null;

        // Match the headword: exact ("essen"), or the noun after its article ("der Hund" ← "hund"),
        // or the last token of a phrase ("sich freuen" ← "freuen").
        var suffix = " " + norm;
        var v = await _db.VocabularyItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.German.ToLower() == norm || x.German.ToLower().EndsWith(suffix), ct);
        if (v is null) return null;

        return new WordLookupDto(word, true, "vocab", v.German, v.English, v.PartOfSpeech,
            v.Article, v.Plural, v.ExampleSentence, v.Note);
    }

    public async Task<WordLookupDto?> GlossAndSaveAsync(string word, string? context, CefrLevel level, CancellationToken ct = default)
    {
        var g = await _lang.LookupWordAsync(word, context, level, ct);
        if (g is null) return null;

        // Save the base form so the next lookup is a free vocab hit. Tagged "Nachschlagen" so
        // looked-up words are identifiable. Deduped against the level's existing words.
        var lvl = await _db.Levels.FirstOrDefaultAsync(l => l.Code == level, ct);
        if (lvl is not null)
        {
            var germ = g.German.Trim();
            if (germ.Length > 0)
            {
                var exists = await _db.VocabularyItems
                    .AnyAsync(x => x.LevelId == lvl.Id && x.German.ToLower() == germ.ToLower(), ct);
                if (!exists)
                {
                    _db.VocabularyItems.Add(new VocabularyItem
                    {
                        LevelId = lvl.Id,
                        German = germ,
                        English = g.English,
                        PartOfSpeech = g.PartOfSpeech ?? "",
                        Article = g.Article,
                        Plural = g.Plural,
                        ExampleSentence = g.Example ?? "",
                        ThemeTag = "Nachschlagen",
                    });
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        return new WordLookupDto(word, true, "ai", g.German, g.English, g.PartOfSpeech,
            g.Article, g.Plural, g.Example, null);
    }
}
