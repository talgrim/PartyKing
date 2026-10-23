using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MadeSlideshowPerSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing images and the one global settings row belong to no session; the image files are removed at startup
            migrationBuilder.Sql("DELETE FROM Images;");
            migrationBuilder.Sql("DELETE FROM SlideshowSettings;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SlideshowSettings",
                table: "SlideshowSettings");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "SlideshowSettings");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "SlideshowSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Images",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_SlideshowSettings",
                table: "SlideshowSettings",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_SessionId_Id",
                table: "Images",
                columns: new[] { "SessionId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Sessions_SessionId",
                table: "Images",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SlideshowSettings_Sessions_SessionId",
                table: "SlideshowSettings",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rows of per-session settings have no place in the single-row table, and the images no longer have a session
            migrationBuilder.Sql("DELETE FROM Images;");
            migrationBuilder.Sql("DELETE FROM SlideshowSettings;");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Sessions_SessionId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_SlideshowSettings_Sessions_SessionId",
                table: "SlideshowSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SlideshowSettings",
                table: "SlideshowSettings");

            migrationBuilder.DropIndex(
                name: "IX_Images_SessionId_Id",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "SlideshowSettings");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Images");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "SlideshowSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SlideshowSettings",
                table: "SlideshowSettings",
                column: "Id");
        }
    }
}
