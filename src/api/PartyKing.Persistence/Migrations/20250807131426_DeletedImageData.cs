using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeletedImageData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Images");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Images",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
