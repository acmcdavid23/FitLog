using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLog.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "WorkoutEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutEntries_SessionId",
                table: "WorkoutEntries",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutEntries_WorkoutSessions_SessionId",
                table: "WorkoutEntries",
                column: "SessionId",
                principalTable: "WorkoutSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutEntries_WorkoutSessions_SessionId",
                table: "WorkoutEntries");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutEntries_SessionId",
                table: "WorkoutEntries");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "WorkoutEntries");
        }
    }
}
