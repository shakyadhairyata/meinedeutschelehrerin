using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Identity;

namespace MeineDeutscheLehrerin.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<PracticeSet> PracticeSets => Set<PracticeSet>();
    public DbSet<PracticeSetItem> PracticeSetItems => Set<PracticeSetItem>();
    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();

    public DbSet<UserLessonProgress> UserLessonProgress => Set<UserLessonProgress>();
    public DbSet<ExerciseAttempt> ExerciseAttempts => Set<ExerciseAttempt>();
    public DbSet<UserVocabularyProgress> UserVocabularyProgress => Set<UserVocabularyProgress>();
    public DbSet<UserStudyPlan> UserStudyPlans => Set<UserStudyPlan>();
    public DbSet<StudyPlanDay> StudyPlanDays => Set<StudyPlanDay>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Level>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Title).HasMaxLength(120);
        });

        b.Entity<Unit>(e =>
        {
            e.HasOne(x => x.Level).WithMany(x => x.Units)
                .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.LevelId, x.Order });
        });

        b.Entity<Lesson>(e =>
        {
            e.HasOne(x => x.Unit).WithMany(x => x.Lessons)
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UnitId, x.Order });
        });

        b.Entity<Exercise>(e =>
        {
            e.HasOne(x => x.Lesson).WithMany(x => x.Exercises)
                .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.LessonId);
        });

        b.Entity<PracticeSet>(e =>
        {
            e.HasOne(x => x.Level).WithMany(x => x.PracticeSets)
                .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PracticeSetItem>(e =>
        {
            e.HasOne(x => x.PracticeSet).WithMany(x => x.Items)
                .HasForeignKey(x => x.PracticeSetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Exercise).WithMany(x => x.PracticeSetItems)
                .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PracticeSetId, x.Order });
        });

        b.Entity<VocabularyItem>(e =>
        {
            e.HasOne(x => x.Level).WithMany(x => x.Vocabulary)
                .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.LevelId, x.ThemeTag });
        });

        b.Entity<UserLessonProgress>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
            e.HasOne(x => x.Lesson).WithMany()
                .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExerciseAttempt>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.AttemptedAt });
            e.HasIndex(x => new { x.UserId, x.Skill });
            e.HasIndex(x => new { x.UserId, x.GrammarTopic });
            e.HasOne(x => x.Exercise).WithMany()
                .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserVocabularyProgress>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.VocabularyItemId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.NextReviewAt });
            e.HasOne(x => x.VocabularyItem).WithMany()
                .HasForeignKey(x => x.VocabularyItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserStudyPlan>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsActive });
            e.HasOne(x => x.Level).WithMany()
                .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StudyPlanDay>(e =>
        {
            e.HasOne(x => x.StudyPlan).WithMany(x => x.Days)
                .HasForeignKey(x => x.StudyPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
