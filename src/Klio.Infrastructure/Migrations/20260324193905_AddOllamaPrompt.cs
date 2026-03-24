using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOllamaPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string defaultPrompt =
                "Liste bis zu 8 kurze beschreibende Tags für dieses Bild auf Deutsch. " +
                "Nutze einfache, allgemeine Begriffe wie Personen, Orte, Objekte, Aktivitäten oder Stimmungen. " +
                "Antworte nur mit einem JSON-Array, keine Erklärung. " +
                "Beispiel: [\"person\",\"draußen\",\"sommer\",\"lachen\"]";

            migrationBuilder.AddColumn<string>(
                name: "OllamaPrompt",
                table: "AppSettings",
                type: "text",
                nullable: false,
                defaultValue: defaultPrompt);

            migrationBuilder.Sql($"UPDATE \"AppSettings\" SET \"OllamaPrompt\" = '{defaultPrompt.Replace("'", "''")}' WHERE \"OllamaPrompt\" IS NULL OR \"OllamaPrompt\" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OllamaPrompt",
                table: "AppSettings");
        }
    }
}
