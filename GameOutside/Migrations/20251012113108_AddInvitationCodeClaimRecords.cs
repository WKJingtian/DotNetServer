using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationCodeClaimRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClaimedInvitationCode",
                table: "UserH5FriendActivityInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationCodeClaimRecords");

            migrationBuilder.DropColumn(
                name: "ClaimedInvitationCode",
                table: "UserH5FriendActivityInfos");
        }
    }
}
