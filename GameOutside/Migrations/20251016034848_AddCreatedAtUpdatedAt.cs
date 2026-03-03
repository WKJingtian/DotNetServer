using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserMonthPassInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserMonthPassInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserH5FriendActivityInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserH5FriendActivityInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserCommodityBoughtRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserCommodityBoughtRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserCards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserCards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "IosGameCenterRewardInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "IosGameCenterRewardInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserMonthPassInfos");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserMonthPassInfos");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserH5FriendActivityInfos");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserH5FriendActivityInfos");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserCommodityBoughtRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserCommodityBoughtRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserCards");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserCards");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "IosGameCenterRewardInfos");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "IosGameCenterRewardInfos");
        }
    }
}
