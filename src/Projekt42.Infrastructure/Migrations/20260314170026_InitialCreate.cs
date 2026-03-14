using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Projekt42.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdminPasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StorageBasePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InboxPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ThumbnailWidth = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailHeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DuplicateLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncomingFilename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DuplicatePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExistingImageId = table.Column<int>(type: "integer", nullable: false),
                    ExistingImagePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalFilename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoredFilename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ThumbnailPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateTaken = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GpsLatitude = table.Column<double>(type: "double precision", nullable: true),
                    GpsLongitude = table.Column<double>(type: "double precision", nullable: true),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Images_Hash",
                table: "Images",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_TopicId",
                table: "Images",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "DuplicateLogs");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}
