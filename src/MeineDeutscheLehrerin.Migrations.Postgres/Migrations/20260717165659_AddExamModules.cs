using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MeineDeutscheLehrerin.Migrations.Postgres.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PracticeSetId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Skill = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    ExerciseId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
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
