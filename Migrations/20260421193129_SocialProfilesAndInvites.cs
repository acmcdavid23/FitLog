using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLog.Migrations
{
    /// <inheritdoc />
    public partial class SocialProfilesAndInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityRegion",
                table: "UserSettings",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                table: "UserSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Groups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Groups",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_InviteCode",
                table: "Groups",
                column: "InviteCode",
                unique: true,
                filter: "[InviteCode] IS NOT NULL");

            migrationBuilder.Sql(@"
UPDATE g SET g.InviteCode = LOWER(REPLACE(CAST(NEWID() AS NVARCHAR(36)), '-', ''))
FROM Groups g
WHERE g.InviteCode IS NULL OR g.InviteCode = N''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Groups_InviteCode",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "ProfileImageUrl",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "CityRegion",
                table: "UserSettings");
        }
    }
}
