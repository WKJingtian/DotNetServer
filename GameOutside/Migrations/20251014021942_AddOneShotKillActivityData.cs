using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddOneShotKillActivityData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityOneShotKills",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    MapCompleteRewardClaimStatus = table.Column<long>(type: "bigint", nullable: false),
                    MapConquerRewardClaimStatus = table.Column<long>(type: "bigint", nullable: false),
                    OneShotKillUltimateRewardClaimStatus = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityOneShotKills", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityOneShotKills");
        }
    }
}
