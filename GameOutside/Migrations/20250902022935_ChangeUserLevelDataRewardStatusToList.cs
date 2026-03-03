using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserLevelDataRewardStatusToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevelData_RewardStatus",
                table: "UserAssets");

            migrationBuilder.AddColumn<List<long>>(
                name: "LevelData_RewardStatusList",
                table: "UserAssets",
                type: "bigint[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevelData_RewardStatusList",
                table: "UserAssets");

            migrationBuilder.AddColumn<long>(
                name: "LevelData_RewardStatus",
                table: "UserAssets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
