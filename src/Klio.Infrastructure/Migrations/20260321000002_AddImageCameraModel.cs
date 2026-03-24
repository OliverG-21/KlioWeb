using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageCameraModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CameraModel",
                table: "Images",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVideo",
                table: "Images",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraModel",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "IsVideo",
                table: "Images");
        }
    }
}
