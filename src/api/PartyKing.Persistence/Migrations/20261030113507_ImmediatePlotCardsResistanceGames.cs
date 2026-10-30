using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImmediatePlotCardsResistanceGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "ResistancePlotCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlayedAt",
                table: "ResistancePlotCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlayedInMission",
                table: "ResistancePlotCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetPlayerId",
                table: "ResistancePlotCards",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "ResistancePlotCards");

            migrationBuilder.DropColumn(
                name: "PlayedAt",
                table: "ResistancePlotCards");

            migrationBuilder.DropColumn(
                name: "PlayedInMission",
                table: "ResistancePlotCards");

            migrationBuilder.DropColumn(
                name: "TargetPlayerId",
                table: "ResistancePlotCards");
        }
    }
}
