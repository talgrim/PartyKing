using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedDeleteAfterPresenataitonFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeleteAfterPresentation",
                table: "Images",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteAfterPresentation",
                table: "Images");
        }
    }
}
