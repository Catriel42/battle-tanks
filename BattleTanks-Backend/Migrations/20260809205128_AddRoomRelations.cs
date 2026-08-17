using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSessionPlayer",
                columns: table => new
                {
                    GameSessionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessionPlayer", x => new { x.GameSessionsId, x.PlayersId });
                    table.ForeignKey(
                        name: "FK_GameSessionPlayer_GameSessions_GameSessionsId",
                        column: x => x.GameSessionsId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameSessionPlayer_Players_PlayersId",
                        column: x => x.PlayersId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionPlayer_PlayersId",
                table: "GameSessionPlayer",
                column: "PlayersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessionPlayer");
        }
    }
}
