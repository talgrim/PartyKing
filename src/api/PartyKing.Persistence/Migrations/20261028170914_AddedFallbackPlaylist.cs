using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedFallbackPlaylist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FallbackCursor",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FallbackPlaylistId",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FallbackPlaylistName",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackShuffle",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFallback",
                table: "QueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackCursor",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "FallbackPlaylistId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "FallbackPlaylistName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "FallbackShuffle",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsFallback",
                table: "QueueItems");
        }
    }
}
