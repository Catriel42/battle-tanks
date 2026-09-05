using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks_Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    TileData = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    TotalKills = table.Column<int>(type: "integer", nullable: false),
                    TotalDeaths = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MapId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    MinPlayers = table.Column<int>(type: "integer", nullable: false),
                    Lives = table.Column<int>(type: "integer", nullable: false),
                    WinnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameSessions_Players_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "PlayerGameStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    ShotsFired = table.Column<int>(type: "integer", nullable: false),
                    ShotsHit = table.Column<int>(type: "integer", nullable: false),
                    BlocksDestroyed = table.Column<int>(type: "integer", nullable: false),
                    DamageDealt = table.Column<int>(type: "integer", nullable: false),
                    DamageTaken = table.Column<int>(type: "integer", nullable: false),
                    FinalPosition = table.Column<int>(type: "integer", nullable: false),
                    SurvivalTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGameStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGameStats_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGameStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Maps",
                columns: new[] { "Id", "CreatedAt", "Height", "Name", "TileData", "Width" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 20, "Classic", "[\n  [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1],\n  [1,0,0,0,0,0,2,2,0,0,1,1,0,0,0,0,0,0,1,1,0,0,2,2,0,0,0,0,0,1],\n  [1,0,1,1,2,0,0,0,0,0,1,1,0,2,2,2,2,0,1,1,0,0,0,0,0,2,1,1,0,1],\n  [1,0,1,1,2,0,1,1,1,0,0,0,0,2,1,1,2,0,0,0,0,1,1,1,0,2,1,1,0,1],\n  [1,0,2,2,0,0,1,1,1,0,2,2,0,2,1,1,2,0,2,2,0,1,1,1,0,0,2,2,0,1],\n  [1,0,0,0,0,0,0,2,0,0,2,2,0,0,0,0,0,0,2,2,0,0,2,0,0,0,0,0,0,1],\n  [1,1,1,0,2,2,0,0,0,0,0,0,0,1,1,1,1,0,0,0,0,0,0,0,2,2,0,1,1,1],\n  [1,1,1,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,0,1,1,1,1,0,1,1,0,1,1,1],\n  [1,0,0,0,1,1,0,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1,1,0,1,1,0,0,0,1],\n  [1,0,2,2,0,0,0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2,0,0,0,0,2,2,0,1],\n  [1,0,2,2,0,0,0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2,0,0,0,0,2,2,0,1],\n  [1,0,0,0,1,1,0,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1,1,0,1,1,0,0,0,1],\n  [1,1,1,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,0,1,1,1,1,0,1,1,0,1,1,1],\n  [1,1,1,0,2,2,0,0,0,0,0,0,0,1,1,1,1,0,0,0,0,0,0,0,2,2,0,1,1,1],\n  [1,0,0,0,0,0,0,2,0,0,2,2,0,0,0,0,0,0,2,2,0,0,2,0,0,0,0,0,0,1],\n  [1,0,2,2,0,0,1,1,1,0,2,2,0,2,1,1,2,0,2,2,0,1,1,1,0,0,2,2,0,1],\n  [1,0,1,1,2,0,1,1,1,0,0,0,0,2,1,1,2,0,0,0,0,1,1,1,0,2,1,1,0,1],\n  [1,0,1,1,2,0,0,0,0,0,1,1,0,2,2,2,2,0,1,1,0,0,0,0,0,2,1,1,0,1],\n  [1,0,0,0,0,0,2,2,0,0,1,1,0,0,0,0,0,0,1,1,0,0,2,2,0,0,0,0,0,1],\n  [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]\n]", 30 });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionPlayer_PlayersId",
                table: "GameSessionPlayer",
                column: "PlayersId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_MapId",
                table: "GameSessions",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_WinnerId",
                table: "GameSessions",
                column: "WinnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_Name",
                table: "Maps",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_GameSessionId",
                table: "PlayerGameStats",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_PlayerId_GameSessionId",
                table: "PlayerGameStats",
                columns: new[] { "PlayerId", "GameSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_Username",
                table: "Players",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessionPlayer");

            migrationBuilder.DropTable(
                name: "PlayerGameStats");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
