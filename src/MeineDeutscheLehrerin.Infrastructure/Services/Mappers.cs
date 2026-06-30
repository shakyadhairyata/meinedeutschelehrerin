using System.Text.Json.Nodes;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

/// <summary>Entity → DTO projections. Exercise solutions are never mapped to client DTOs.</summary>
public static class Mappers
{
    public static JsonNode? ParseContent(string json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    public static ExerciseDto ToDto(this Exercise e) => new(
        e.Id, e.Type, e.Skill, e.Prompt, ParseContent(e.ContentJson),
        e.GrammarTopic, e.Points, e.Difficulty);

    public static VocabularyDto ToDto(this VocabularyItem v, UserVocabularyProgress? p = null) => new(
        v.Id, v.German, v.English, v.PartOfSpeech, v.Article, v.Plural,
        v.ExampleSentence, v.Note, v.ThemeTag, p?.Box ?? 0, p?.NextReviewAt);

    public static PracticeSetDto ToDto(this PracticeSet s, int exerciseCount) => new(
        s.Id, s.Title, s.Description, s.Skill, s.Kind, s.IsExam, s.TimeLimitMinutes, exerciseCount);
}
