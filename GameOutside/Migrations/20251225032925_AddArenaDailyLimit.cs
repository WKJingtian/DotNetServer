using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddArenaDailyLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastArenaBoxRewardTime",
                table: "UserGameInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TodayArenaBoxRewardCount",
                table: "UserGameInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastArenaBoxRewardTime",
                table: "UserGameInfos");

            migrationBuilder.DropColumn(
                name: "TodayArenaBoxRewardCount",
                table: "UserGameInfos");
        }
    }
}
