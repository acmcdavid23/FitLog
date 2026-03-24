using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLog.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplementLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalorieGoal = table.Column<int>(type: "int", nullable: false),
                    ProteinGoal = table.Column<int>(type: "int", nullable: false),
                    CarbGoal = table.Column<int>(type: "int", nullable: false),
                    FatGoal = table.Column<int>(type: "int", nullable: false),
                    WaterGoal = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WeightUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FitnessGoal = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
