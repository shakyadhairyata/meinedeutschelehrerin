using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeineDeutscheLehrerin.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LookupCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Word = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    German = table.Column<string>(type: "TEXT", nullable: false),
                    English = table.Column<string>(type: "TEXT", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "TEXT", nullable: false),
                    Article = table.Column<string>(type: "TEXT", nullable: true),
                    Plural = table.Column<string>(type: "TEXT", nullable: true),
                    Example = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LookupCache_Word",
                table: "LookupCache",
                column: "Word",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LookupCache");
        }
    }
}
