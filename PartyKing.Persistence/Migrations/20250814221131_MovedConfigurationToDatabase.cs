using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovedConfigurationToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WasPresented",
                table: "Images",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SlideshowSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoRepeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    SlideTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    StartFromBeginning = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlideshowSettings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988505-d093-7975-b654-e08ccf29268f"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-1bb9-7308-a454-183052930ad6"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-4eee-760b-aea7-9cbab5f126f6"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-9009-7274-9825-a38a703ec929"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-bfaf-7985-b161-d1555555a762"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-0487-7618-bc80-bbecfec7479c"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-2f0c-758b-b577-65ceeb9b1dbe"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-52ae-71df-9a53-918a85a61a25"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-ac6b-73ae-a586-902f2a46f4b0"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-f7bc-73cc-8505-12313d4419e3"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-708d-aa86-4c8edb216623"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-718b-affa-b04bdd32f330"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-73dd-ae67-29b2edf9f268"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-744e-9811-eca74c350a98"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-76f2-a1d2-bfd6c4c213df"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7855-9c0c-52c5524614b4"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-79da-8602-c6c2c11dae72"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7be5-a608-95918f46d42c"),
                column: "WasPresented",
                value: false);

            migrationBuilder.UpdateData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7bfd-94a8-8d8aad6cdeb2"),
                column: "WasPresented",
                value: false);

            migrationBuilder.InsertData(
                table: "SlideshowSettings",
                columns: new[] { "Id", "AutoRepeat", "SlideTime", "StartFromBeginning" },
                values: new object[] { 1, false, new TimeSpan(0, 0, 0, 20, 0), false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlideshowSettings");

            migrationBuilder.DropColumn(
                name: "WasPresented",
                table: "Images");
        }
    }
}
