using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumFolderStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Topics",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlbumFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentFolderId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbumFolders_AlbumFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "AlbumFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_FolderId",
                table: "Topics",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumFolders_ParentFolderId",
                table: "AlbumFolders",
                column: "ParentFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_AlbumFolders_FolderId",
                table: "Topics",
                column: "FolderId",
                principalTable: "AlbumFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Topics_AlbumFolders_FolderId",
                table: "Topics");

            migrationBuilder.DropTable(
                name: "AlbumFolders");

            migrationBuilder.DropIndex(
                name: "IX_Topics_FolderId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Topics");
        }
    }
}
