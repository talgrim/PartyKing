using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlotCardsResistanceGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResistancePlotCards",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<int>(type: "INTEGER", nullable: false),
                    HolderPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeckOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResistancePlotCards", x => new { x.SessionId, x.CardId });
                    table.ForeignKey(
                        name: "FK_ResistancePlotCards_ResistanceGames_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ResistanceGames",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResistancePlotCards");
        }
    }
}
