using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VotedResistanceGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Vote",
                table: "ResistancePlayers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Phase",
                table: "ResistanceGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RejectedTeams",
                table: "ResistanceGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Winner",
                table: "ResistanceGames",
                type: "INTEGER",
                nullable: true);

            // A team that was submitted before voting existed now waits for its vote (Phase 1 = Voting).
            migrationBuilder.Sql(
                """
                UPDATE "ResistanceGames" SET "Phase" = 1
                WHERE EXISTS (
                    SELECT 1 FROM "ResistancePlayers" p
                    WHERE p."SessionId" = "ResistanceGames"."SessionId" AND p."IsOnTeam" = 1)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vote",
                table: "ResistancePlayers");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "ResistanceGames");

            migrationBuilder.DropColumn(
                name: "RejectedTeams",
                table: "ResistanceGames");

            migrationBuilder.DropColumn(
                name: "Winner",
                table: "ResistanceGames");
        }
    }
}
