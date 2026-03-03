using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInfoWorldRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<int>>(
                name: "WorldRankHistories",
                table: "UserInfos",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AddColumn<List<int>>(
                name: "WorldRankSeasonHistories",
                table: "UserInfos",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]");

            migrationBuilder.AlterColumn<long>(
                name: "Score",
                table: "UserHistories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorldRankHistories",
                table: "UserInfos");

            migrationBuilder.DropColumn(
                name: "WorldRankSeasonHistories",
                table: "UserInfos");

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "UserHistories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
