using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeineDeutscheLehrerin.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExamModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PracticeSetModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PracticeSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Skill = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSetModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSetModules_PracticeSets_PracticeSetId",
                        column: x => x.PracticeSetId,
                        principalTable: "PracticeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSetModuleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSetModuleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSetModuleItems_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PracticeSetModuleItems_PracticeSetModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "PracticeSetModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSetModuleItems_ExerciseId",
                table: "PracticeSetModuleItems",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSetModuleItems_ModuleId_Order",
                table: "PracticeSetModuleItems",
                columns: new[] { "ModuleId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSetModules_PracticeSetId_Order",
                table: "PracticeSetModules",
                columns: new[] { "PracticeSetId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PracticeSetModuleItems");

            migrationBuilder.DropTable(
                name: "PracticeSetModules");
        }
    }
}
