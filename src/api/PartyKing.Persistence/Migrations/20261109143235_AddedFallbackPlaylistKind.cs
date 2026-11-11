using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedFallbackPlaylistKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FallbackPlaylistKind",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            // Every playlist set so far is a Spotify one
            migrationBuilder.Sql(
                """
                UPDATE "Sessions" SET "FallbackPlaylistKind" = 'Spotify' WHERE "FallbackPlaylistId" IS NOT NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackPlaylistKind",
                table: "Sessions");
        }
    }
}
