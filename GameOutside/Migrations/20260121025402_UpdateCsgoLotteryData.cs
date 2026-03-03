using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCsgoLotteryData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiamondPurchaseCountRefreshTime",
                table: "ActivityCsgoStyleLotteryInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<List<long>>(
                name: "RewardRecordTime",
                table: "ActivityCsgoStyleLotteryInfos",
                type: "bigint[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::bigint[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiamondPurchaseCountRefreshTime",
                table: "ActivityCsgoStyleLotteryInfos");

            migrationBuilder.DropColumn(
                name: "RewardRecordTime",
                table: "ActivityCsgoStyleLotteryInfos");
        }
    }
}
