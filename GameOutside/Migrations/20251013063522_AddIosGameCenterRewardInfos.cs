using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddIosGameCenterRewardInfos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitationCodeClaimRecords",
                columns: table => new
                {
                    GiftCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationCodeClaimRecords", x => x.GiftCode);
                });

            migrationBuilder.CreateTable(
                name: "IosGameCenterRewardInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    RewardClaimed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IosGameCenterRewardInfos", x => new { x.PlayerId, x.ShardId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationCodeClaimRecords");

            migrationBuilder.DropTable(
                name: "IosGameCenterRewardInfos");
        }
    }
}
