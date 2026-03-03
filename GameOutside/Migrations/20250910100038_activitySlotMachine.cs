using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class activitySlotMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivitySlotMachines",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    LastDrawTime = table.Column<long>(type: "bigint", nullable: false),
                    TodayDrawCount = table.Column<int>(type: "integer", nullable: false),
                    ActivityPoint = table.Column<int>(type: "integer", nullable: false),
                    PointRewardClaimStatus = table.Column<long>(type: "bigint", nullable: false),
                    RewardsInSlot = table.Column<List<int>>(type: "integer[]", nullable: false),
                    RerollCounts = table.Column<List<int>>(type: "integer[]", nullable: false),
                    GuaranteeProgressList = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySlotMachines", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivitySlotMachines");
        }
    }
}
