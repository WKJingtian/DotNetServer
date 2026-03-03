using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddUserH5FriendActivityInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserH5FriendActivityInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    NextShowingLevel = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserH5FriendActivityInfos", x => new { x.PlayerId, x.ShardId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserH5FriendActivityInfos");
        }
    }
}
