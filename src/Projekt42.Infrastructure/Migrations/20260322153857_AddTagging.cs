using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Projekt42.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTagging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OllamaModel",
                table: "AppSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OllamaUrl",
                table: "AppSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "TaggingEnabled",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ImageTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageId = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageTags_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageTags_ImageId",
                table: "ImageTags",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageTags_Tag",
                table: "ImageTags",
                column: "Tag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageTags");

            migrationBuilder.DropColumn(
                name: "OllamaModel",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OllamaUrl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TaggingEnabled",
                table: "AppSettings");
        }
    }
}
