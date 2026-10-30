using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissionsResistanceGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MissionCard",
                table: "ResistancePlayers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResistanceMissionResults",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MissionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FailCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResistanceMissionResults", x => new { x.SessionId, x.MissionNumber });
                    table.ForeignKey(
                        name: "FK_ResistanceMissionResults_ResistanceGames_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ResistanceGames",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            // A game parked on an approved team (VoteResult = 2) now plays its mission (PlayingMission = 4)
            migrationBuilder.Sql(
                @"UPDATE ResistanceGames SET Phase = 4
                  WHERE Phase = 2
                    AND (SELECT COUNT(*) FROM ResistancePlayers p WHERE p.SessionId = ResistanceGames.SessionId AND p.Vote = 1) * 2
                      > (SELECT COUNT(*) FROM ResistancePlayers p WHERE p.SessionId = ResistanceGames.SessionId)");

            // A game parked on a rejected team (VoteResult = 2, no longer used) lets the same leader pick again
            migrationBuilder.Sql(
                @"UPDATE ResistancePlayers SET IsOnTeam = 0
                  WHERE SessionId IN (SELECT SessionId FROM ResistanceGames WHERE Phase = 2)");
            migrationBuilder.Sql("UPDATE ResistanceGames SET Phase = 0 WHERE Phase = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResistanceMissionResults");

            migrationBuilder.DropColumn(
                name: "MissionCard",
                table: "ResistancePlayers");
        }
    }
}
