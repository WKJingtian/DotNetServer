using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddActicityTreasureMazeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityTreasureMazeInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    GameKeyCount = table.Column<int>(type: "integer", nullable: false),
                    LastGameKeyTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    AwayGameCountToday = table.Column<int>(type: "integer", nullable: false),
                    LastAwayGameTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    LevelPassed = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTreasureMazeInfos", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityTreasureMazeInfos");
        }
    }
}
