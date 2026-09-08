using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_CreatedAt",
                table: "PlayerGameStats",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_Kills",
                table: "PlayerGameStats",
                column: "Kills");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_PlayerId",
                table: "PlayerGameStats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_CreatedAt",
                table: "GameSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status_CreatedAt",
                table: "GameSessions",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerGameStats_CreatedAt",
                table: "PlayerGameStats");

            migrationBuilder.DropIndex(
                name: "IX_PlayerGameStats_Kills",
                table: "PlayerGameStats");

            migrationBuilder.DropIndex(
                name: "IX_PlayerGameStats_PlayerId",
                table: "PlayerGameStats");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_CreatedAt",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Status_CreatedAt",
                table: "GameSessions");
        }
    }
}
