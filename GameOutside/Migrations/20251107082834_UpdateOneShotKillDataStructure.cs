using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOneShotKillDataStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MapConquerRewardClaimStatus",
                table: "ActivityOneShotKills",
                newName: "TaskCompleteRewardClaimStatus");

            migrationBuilder.AddColumn<long>(
                name: "AwayGameCountUpdateTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "GameCountUpdateTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<List<long>>(
                name: "MapConquerRewardClaimTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint[]",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "TodayAwayGameCount",
                table: "ActivityOneShotKills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TodayGameCount",
                table: "ActivityOneShotKills",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayGameCountUpdateTimestamp",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "GameCountUpdateTimestamp",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "MapConquerRewardClaimTimestamp",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "TodayAwayGameCount",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "TodayGameCount",
                table: "ActivityOneShotKills");

            migrationBuilder.RenameColumn(
                name: "TaskCompleteRewardClaimStatus",
                table: "ActivityOneShotKills",
                newName: "MapConquerRewardClaimStatus");
        }
    }
}
