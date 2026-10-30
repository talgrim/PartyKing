using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedRingoFlipHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingCardId",
                table: "RingoGames",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RingoFlips",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    FlippedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FlippedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingoFlips", x => new { x.SessionId, x.Order });
                    table.ForeignKey(
                        name: "FK_RingoFlips_RingoGames_SessionId",
                        column: x => x.SessionId,
                        principalTable: "RingoGames",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingoFlips_SessionId_CardId",
                table: "RingoFlips",
                columns: new[] { "SessionId", "CardId" },
                unique: true);

            // Carry over the flips of games in progress (the comma separated card ids), flipped at the game's start
            migrationBuilder.Sql(
                """
                WITH RECURSIVE split(SessionId, "Order", CardId, Rest) AS (
                    SELECT SessionId, 0,
                           CAST(substr(Rest, 1, instr(Rest, ',') - 1) AS INTEGER),
                           substr(Rest, instr(Rest, ',') + 1)
                    FROM (SELECT SessionId, FlippedCards || ',' AS Rest FROM RingoGames WHERE FlippedCards <> '')
                    UNION ALL
                    SELECT SessionId, "Order" + 1,
                           CAST(substr(Rest, 1, instr(Rest, ',') - 1) AS INTEGER),
                           substr(Rest, instr(Rest, ',') + 1)
                    FROM split WHERE Rest <> ''
                )
                INSERT INTO RingoFlips (SessionId, "Order", CardId, FlippedAt, FlippedBy)
                SELECT s.SessionId, s."Order", s.CardId, g.CreatedAt, 'admin'
                FROM split s JOIN RingoGames g ON g.SessionId = s.SessionId;
                """);

            migrationBuilder.DropColumn(
                name: "FlippedCards",
                table: "RingoGames");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingoFlips");

            migrationBuilder.DropColumn(
                name: "PendingCardId",
                table: "RingoGames");

            migrationBuilder.AddColumn<string>(
                name: "FlippedCards",
                table: "RingoGames",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
