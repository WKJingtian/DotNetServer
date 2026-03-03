using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddDrawLimitForCsgoLottery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DiamondPurchaseCountRefreshTime",
                table: "ActivityCsgoStyleLotteryInfos",
                newName: "LotteryDrawInfoRefreshTimestamp");

            migrationBuilder.AddColumn<int>(
                name: "TotalLotteryDrawToday",
                table: "ActivityCsgoStyleLotteryInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalLotteryDrawToday",
                table: "ActivityCsgoStyleLotteryInfos");

            migrationBuilder.RenameColumn(
                name: "LotteryDrawInfoRefreshTimestamp",
                table: "ActivityCsgoStyleLotteryInfos",
                newName: "DiamondPurchaseCountRefreshTime");
        }
    }
}
