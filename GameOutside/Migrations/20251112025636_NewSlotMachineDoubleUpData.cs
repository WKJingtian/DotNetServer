using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class NewSlotMachineDoubleUpData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextDrawRewardDoubledUp",
                table: "ActivitySlotMachines");

            migrationBuilder.AlterColumn<List<int>>(
                name: "RewardsInSlot",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]",
                oldClrType: typeof(List<int>),
                oldType: "integer[]");

            migrationBuilder.AlterColumn<List<int>>(
                name: "RerollCounts",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]",
                oldClrType: typeof(List<int>),
                oldType: "integer[]");

            migrationBuilder.AlterColumn<List<int>>(
                name: "GuaranteeProgressList",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]",
                oldClrType: typeof(List<int>),
                oldType: "integer[]");

            migrationBuilder.AddColumn<List<int>>(
                name: "RewardDoubledUpItemCount",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AlterColumn<List<long>>(
                name: "MapConquerRewardClaimTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::bigint[]",
                oldClrType: typeof(List<long>),
                oldType: "bigint[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RewardDoubledUpItemCount",
                table: "ActivitySlotMachines");

            migrationBuilder.AlterColumn<List<int>>(
                name: "RewardsInSlot",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(List<int>),
                oldType: "integer[]",
                oldDefaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AlterColumn<List<int>>(
                name: "RerollCounts",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(List<int>),
                oldType: "integer[]",
                oldDefaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AlterColumn<List<int>>(
                name: "GuaranteeProgressList",
                table: "ActivitySlotMachines",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(List<int>),
                oldType: "integer[]",
                oldDefaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AddColumn<bool>(
                name: "NextDrawRewardDoubledUp",
                table: "ActivitySlotMachines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<List<long>>(
                name: "MapConquerRewardClaimTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint[]",
                nullable: false,
                oldClrType: typeof(List<long>),
                oldType: "bigint[]",
                oldDefaultValueSql: "ARRAY[]::bigint[]");
        }
    }
}
