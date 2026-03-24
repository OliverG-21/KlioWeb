using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderInboxPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FolderInboxPath",
                table: "AppSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderInboxPath",
                table: "AppSettings");
        }
    }
}
