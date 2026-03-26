using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLog.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendsAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "SupplementLibraryItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemItem",
                table: "SupplementLibraryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Exercises",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "SupplementLibraryItems");

            migrationBuilder.DropColumn(
                name: "IsSystemItem",
                table: "SupplementLibraryItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Exercises");
        }
    }
}
