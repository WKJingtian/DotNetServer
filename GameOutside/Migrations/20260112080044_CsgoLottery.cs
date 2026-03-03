using System;
using System.Collections.Generic;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class CsgoLottery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityCsgoStyleLotteryInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    ActivityPoint = table.Column<int>(type: "integer", nullable: false),
                    PointRewardClaimStatus = table.Column<long>(type: "bigint", nullable: false),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    KeyPurchaseCountByDiamond = table.Column<int>(type: "integer", nullable: false),
                    ActivityPremiumPassStatus = table.Column<long>(type: "bigint", nullable: false),
                    PremiumPassDailyRewardClaimStatus = table.Column<List<long>>(type: "bigint[]", nullable: false, defaultValueSql: "ARRAY[]::bigint[]"),
                    RewardRecord = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]"),
                    TaskRecord = table.Column<Dictionary<string, CsgoStyleLotteryTask>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCsgoStyleLotteryInfos", x => new { x.PlayerId, x.ActivityId, x.ShardId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityCsgoStyleLotteryInfos");
        }
    }
}
