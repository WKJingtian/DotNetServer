using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEncryptionInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityPiggyBanks",
                table: "ActivityPiggyBanks");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityPiggyBanks",
                table: "ActivityPiggyBanks",
                columns: new[] { "PlayerId", "ShardId" });

            migrationBuilder.CreateTable(
                name: "UserEncryptionInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    EncryptionKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEncryptionInfos", x => new { x.PlayerId, x.ShardId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEncryptionInfos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityPiggyBanks",
                table: "ActivityPiggyBanks");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityPiggyBanks",
                table: "ActivityPiggyBanks",
                columns: new[] { "PlayerId", "ShardId", "ActivityId" });
        }
    }
}
