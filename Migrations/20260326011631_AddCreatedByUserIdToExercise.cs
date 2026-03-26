using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLog.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserIdToExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyGoal",
                table: "UserSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentWeight",
                table: "UserSettings",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GoalTimeframeWeeks",
                table: "UserSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "GoalWeight",
                table: "UserSettings",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightInches",
                table: "UserSettings",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyGoal",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "CurrentWeight",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "GoalTimeframeWeeks",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "GoalWeight",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "HeightInches",
                table: "UserSettings");
        }
    }
}
