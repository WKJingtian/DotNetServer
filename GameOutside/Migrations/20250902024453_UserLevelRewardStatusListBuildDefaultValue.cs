using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class UserLevelRewardStatusListBuildDefaultValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<long>>(
                name: "LevelData_RewardStatusList",
                table: "UserAssets",
                type: "bigint[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::bigint[]",
                oldClrType: typeof(List<long>),
                oldType: "bigint[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<long>>(
                name: "LevelData_RewardStatusList",
                table: "UserAssets",
                type: "bigint[]",
                nullable: false,
                oldClrType: typeof(List<long>),
                oldType: "bigint[]",
                oldDefaultValueSql: "ARRAY[]::bigint[]");
        }
    }
}
