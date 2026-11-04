using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReclaimableSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaimStamp",
                table: "ResistancePlayers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminSeat",
                table: "ResistancePlayers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReclaimable",
                table: "ResistancePlayers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimStamp",
                table: "ResistancePlayers");

            migrationBuilder.DropColumn(
                name: "IsAdminSeat",
                table: "ResistancePlayers");

            migrationBuilder.DropColumn(
                name: "IsReclaimable",
                table: "ResistancePlayers");
        }
    }
}
