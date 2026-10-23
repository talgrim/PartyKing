using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedQueueItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "SpotifyProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateTable(
                name: "QueueItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Artists = table.Column<string>(type: "TEXT", nullable: true),
                    AlbumArtUrl = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueueItems_Status",
                table: "QueueItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueueItems");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "SpotifyProfiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
