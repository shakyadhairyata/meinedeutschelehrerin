using System.Text.Json;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;

namespace MeineDeutscheLehrerin.Infrastructure.Seeding;

/// <summary>
/// Fluent helper that builds the curriculum object graph in memory. The whole graph is
/// reachable from <see cref="Levels"/>, so a single AddRange + SaveChanges persists it.
/// Order fields are assigned automatically from sibling counts.
/// </summary>
public class CurriculumBuilder
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public List<Level> Levels { get; } = new();

    public Level Level(CefrLevel code, string title, string desc, string goethe, int days = 14, int minutes = 90)
    {
        var l = new Level
        {
            Code = code, Title = title, Description = desc, GoetheExam = goethe,
            Order = (int)code, EstimatedDays = days, RecommendedMinutesPerDay = minutes
        };
        Levels.Add(l);
        return l;
    }

    public Unit Unit(Level level, string title, string desc, string theme)
    {
        var u = new Unit { Title = title, Description = desc, ThemeTag = theme, Order = level.Units.Count + 1 };
        level.Units.Add(u);
        return u;
    }

    public Lesson Lesson(Unit unit, string title, SkillType skill, string content,
        string? grammarTopic = null, string? audio = null, int minutes = 15)
    {
        var le = new Lesson
        {
            Title = title, Skill = skill, Content = content, GrammarTopic = grammarTopic,
            AudioScript = audio, EstimatedMinutes = minutes, Order = unit.Lessons.Count + 1
        };
        unit.Lessons.Add(le);
        return le;
    }

    public Exercise Ex(Lesson lesson, ExerciseType type, SkillType skill, string prompt,
        object content, object solution, string explanation,
        string? grammarTopic = null, int points = 10, int difficulty = 1)
    {
        var e = new Exercise
        {
            Type = type, Skill = skill, Prompt = prompt,
            ContentJson = JsonSerializer.Serialize(content, J),
            SolutionJson = JsonSerializer.Serialize(solution, J),
            Explanation = explanation,
            GrammarTopic = grammarTopic ?? lesson.GrammarTopic,
            Points = points, Difficulty = difficulty, Order = lesson.Exercises.Count + 1
        };
        lesson.Exercises.Add(e);
        return e;
    }

    public VocabularyItem Vocab(Level level, string de, string en, string pos,
        string? article, string? plural, string example, string theme, string? note = null)
    {
        var v = new VocabularyItem
        {
            German = de, English = en, PartOfSpeech = pos, Article = article, Plural = plural,
            ExampleSentence = example, ThemeTag = theme, Note = note
        };
        level.Vocabulary.Add(v);
        return v;
    }

    public PracticeSet Set(Level level, string title, string desc, string kind, SkillType? skill,
        bool isExam, int? timeLimit, params Exercise[] exercises)
    {
        var s = new PracticeSet
        {
            Title = title, Description = desc, Kind = kind, Skill = skill,
            IsExam = isExam, TimeLimitMinutes = timeLimit, Order = level.PracticeSets.Count + 1
        };
        int o = 1;
        foreach (var ex in exercises) s.Items.Add(new PracticeSetItem { Exercise = ex, Order = o++ });
        level.PracticeSets.Add(s);
        return s;
    }
}
