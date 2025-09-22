using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Galleria.Migrations
{
    /// <inheritdoc />
    public partial class AddShareTokenToMediaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "MediaItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "MediaItems");
        }
    }
}
