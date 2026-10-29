using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResistanceRulesSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "ResistanceGames",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"PlayerCounts\":{\"5\":{\"Spies\":2,\"TeamSizes\":[2,3,2,3,3],\"RequiredFails\":[1,1,1,1,1]},\"6\":{\"Spies\":2,\"TeamSizes\":[2,3,4,3,4],\"RequiredFails\":[1,1,1,1,1]},\"7\":{\"Spies\":3,\"TeamSizes\":[2,3,3,4,4],\"RequiredFails\":[1,1,1,2,1]},\"8\":{\"Spies\":3,\"TeamSizes\":[3,4,4,5,5],\"RequiredFails\":[1,1,1,2,1]},\"9\":{\"Spies\":3,\"TeamSizes\":[3,4,4,5,5],\"RequiredFails\":[1,1,1,2,1]},\"10\":{\"Spies\":4,\"TeamSizes\":[3,4,4,5,5],\"RequiredFails\":[1,1,1,2,1]}}}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rules",
                table: "ResistanceGames");
        }
    }
}
