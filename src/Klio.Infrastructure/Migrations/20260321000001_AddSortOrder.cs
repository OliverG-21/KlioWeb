using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    public partial class AddSortOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Topics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "AlbumFolders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SortOrder", table: "Topics");
            migrationBuilder.DropColumn(name: "SortOrder", table: "AlbumFolders");
        }
    }
}
