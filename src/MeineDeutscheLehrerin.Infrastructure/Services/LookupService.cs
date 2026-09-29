using System.Linq;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface ILookupService
{
    /// <summary>Free, offline: match a word against the curated vocabulary and then the lookup cache
    /// (previously looked-up words). Null on a miss.</summary>
    Task<WordLookupDto?> MatchAsync(string word, CancellationToken ct = default);

    /// <summary>Miss path: gloss the word via the language-service (AI) and cache it by its surface
    /// form, so the SAME word — inflected forms included — is free next time. The cache is separate
    /// from the vocabulary, so looked-up words never enter the study/SRS deck. Null if unresolved.</summary>
    Task<WordLookupDto?> GlossAndCacheAsync(string word, string? context, CefrLevel level, CancellationToken ct = default);
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

    public async Task<WordLookupDto?> MatchAsync(string word, CancellationToken ct = default)
    {
        var norm = Normalize(word);
        if (norm.Length == 0) return null;

        // 1) Curated vocabulary: exact ("essen"), noun after its article ("der Hund" ← "hund"),
        //    or the last token of a phrase ("sich freuen" ← "freuen").
        var suffix = " " + norm;
        var v = await _db.VocabularyItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.German.ToLower() == norm || x.German.ToLower().EndsWith(suffix), ct);
        if (v is not null)
            return new WordLookupDto(word, true, "vocab", v.German, v.English, v.PartOfSpeech,
                v.Article, v.Plural, v.ExampleSentence, v.Note);

        // 2) Lookup cache: this exact surface form was glossed before (free, no AI).
        var c = await _db.LookupCache.AsNoTracking().FirstOrDefaultAsync(x => x.Word == norm, ct);
        if (c is not null)
            return new WordLookupDto(word, true, "cache", c.German, c.English, c.PartOfSpeech,
                c.Article, c.Plural, c.Example, null);

        return null;
    }

    public async Task<WordLookupDto?> GlossAndCacheAsync(string word, string? context, CefrLevel level, CancellationToken ct = default)
    {
        var g = await _lang.LookupWordAsync(word, context, level, ct);
        if (g is null) return null;

        // Cache by the SURFACE form, so this exact word is free next time — not in the vocab deck.
        var norm = Normalize(word);
        if (norm.Length > 0 && !await _db.LookupCache.AnyAsync(x => x.Word == norm, ct))
        {
            _db.LookupCache.Add(new WordLookupEntry
            {
                Word = norm,
                German = g.German.Trim(),
                English = g.English,
                PartOfSpeech = g.PartOfSpeech ?? "",
                Article = g.Article,
                Plural = g.Plural,
                Example = g.Example ?? "",
            });
            await _db.SaveChangesAsync(ct);
        }

        return new WordLookupDto(word, true, "ai", g.German, g.English, g.PartOfSpeech,
            g.Article, g.Plural, g.Example, null);
    }
}
