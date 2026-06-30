using System.Text.Json.Nodes;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Services;
using Xunit;

namespace MeineDeutscheLehrerin.Tests;

public class ExerciseGraderTests
{
    private readonly ExerciseGrader _grader = new();

    private static Exercise Ex(ExerciseType type, string content, string solution) =>
        new() { Type = type, Skill = SkillType.Grammar, ContentJson = content, SolutionJson = solution };

    private static JsonNode? N(string json) => JsonNode.Parse(json);

    [Fact]
    public void MultipleChoice_correct_scores_100()
    {
        var ex = Ex(ExerciseType.MultipleChoice, """{"question":"q","options":["a","b","c"]}""", """{"correctIndex":2}""");
        var r = _grader.Grade(ex, N("""{"selectedIndex":2}"""));
        Assert.True(r.IsCorrect);
        Assert.Equal(100, r.ScorePercent);
    }

    [Fact]
    public void MultipleChoice_wrong_scores_0()
    {
        var ex = Ex(ExerciseType.MultipleChoice, """{"options":["a","b"]}""", """{"correctIndex":1}""");
        var r = _grader.Grade(ex, N("""{"selectedIndex":0}"""));
        Assert.False(r.IsCorrect);
        Assert.Equal(0, r.ScorePercent);
    }

    [Fact]
    public void FillInBlank_all_blanks_correct()
    {
        var ex = Ex(ExerciseType.FillInBlank, """{"text":"Ich ___ und du ___."}""", """{"answers":[["bin"],["bist"]]}""");
        var r = _grader.Grade(ex, N("""{"answers":["bin","bist"]}"""));
        Assert.True(r.IsCorrect);
        Assert.Equal(100, r.ScorePercent);
    }

    [Fact]
    public void FillInBlank_partial_gives_proportional_score()
    {
        var ex = Ex(ExerciseType.FillInBlank, """{"text":"Ich ___ und du ___."}""", """{"answers":[["bin"],["bist"]]}""");
        var r = _grader.Grade(ex, N("""{"answers":["bin","ist"]}"""));
        Assert.False(r.IsCorrect);
        Assert.Equal(50, r.ScorePercent);
    }

    [Fact]
    public void FillInBlank_accepts_any_listed_alternative_and_is_case_insensitive()
    {
        var ex = Ex(ExerciseType.FillInBlank, """{"text":"___"}""", """{"answers":[["heiße","bin"]]}""");
        var r = _grader.Grade(ex, N("""{"answers":["BIN"]}"""));
        Assert.True(r.IsCorrect);
    }

    [Fact]
    public void Reorder_correct_via_text()
    {
        var ex = Ex(ExerciseType.Reorder, """{"tokens":["bin","Ich","Student"]}""", """{"answer":"Ich bin Student"}""");
        var r = _grader.Grade(ex, N("""{"text":"Ich bin Student"}"""));
        Assert.True(r.IsCorrect);
    }

    [Fact]
    public void Reorder_correct_via_tokens_ignoring_punctuation()
    {
        var ex = Ex(ExerciseType.Reorder, """{"tokens":["kommst","Woher","du","?"]}""", """{"answer":"Woher kommst du?"}""");
        var r = _grader.Grade(ex, N("""{"tokens":["Woher","kommst","du","?"]}"""));
        Assert.True(r.IsCorrect);
    }

    [Fact]
    public void Matching_all_pairs_correct()
    {
        var ex = Ex(ExerciseType.Matching, """{"left":["a","b"],"right":["x","y"]}""", """{"pairs":[[0,1],[1,0]]}""");
        var r = _grader.Grade(ex, N("""{"pairs":[[0,1],[1,0]]}"""));
        Assert.True(r.IsCorrect);
        Assert.Equal(100, r.ScorePercent);
    }

    [Fact]
    public void Matching_partial_is_not_fully_correct()
    {
        var ex = Ex(ExerciseType.Matching, """{"left":["a","b"],"right":["x","y"]}""", """{"pairs":[[0,1],[1,0]]}""");
        var r = _grader.Grade(ex, N("""{"pairs":[[0,1],[1,1]]}"""));
        Assert.False(r.IsCorrect);
        Assert.Equal(50, r.ScorePercent);
    }

    [Fact]
    public void Conjugation_is_case_insensitive()
    {
        var ex = Ex(ExerciseType.Conjugation, """{"verb":"sein","person":"du"}""", """{"answers":["bist"]}""");
        var r = _grader.Grade(ex, N("""{"answer":"Bist"}"""));
        Assert.True(r.IsCorrect);
    }

    [Fact]
    public void Dictation_accepts_missing_final_punctuation()
    {
        var ex = Ex(ExerciseType.Dictation, """{"audioText":"Ich heiße Tom."}""", """{"text":"Ich heiße Tom."}""");
        var r = _grader.Grade(ex, N("""{"text":"Ich heiße Tom"}"""));
        Assert.True(r.IsCorrect);
    }

    [Fact]
    public void Translation_accepts_a_listed_alternative()
    {
        var ex = Ex(ExerciseType.Translation, """{"source":"I am a student"}""", """{"answers":["Ich bin Student","Ich bin ein Student"]}""");
        var r = _grader.Grade(ex, N("""{"text":"Ich bin ein Student"}"""));
        Assert.True(r.IsCorrect);
    }

    [Theory]
    [InlineData(ExerciseType.Writing)]
    [InlineData(ExerciseType.Speaking)]
    public void Writing_and_Speaking_are_not_auto_gradable(ExerciseType type)
    {
        Assert.False(_grader.CanAutoGrade(type));
    }

    [Theory]
    [InlineData(ExerciseType.MultipleChoice)]
    [InlineData(ExerciseType.FillInBlank)]
    [InlineData(ExerciseType.Reorder)]
    public void Auto_gradable_types_report_true(ExerciseType type)
    {
        Assert.True(_grader.CanAutoGrade(type));
    }
}
