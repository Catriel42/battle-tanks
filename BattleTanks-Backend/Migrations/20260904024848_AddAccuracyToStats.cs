using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAccuracyToStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Accuracy",
                table: "PlayerGameStats",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accuracy",
                table: "PlayerGameStats");
        }
    }
}
