using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoverImageId",
                table: "Topics",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "Topics");
        }
    }
}
