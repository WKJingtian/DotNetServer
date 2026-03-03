using System;
using AssistActivity.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddNextShowingLevelV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextShowingLevelV1",
                table: "UserH5FriendActivityInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "PlayerAssistActivityInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    DistroId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityName = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<PlayerAssistActivityInfo.AssistActivityPayload>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAssistActivityInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAssistActivityInfos_PlayerId_ShardId_DistroId",
                table: "PlayerAssistActivityInfos",
                columns: new[] { "PlayerId", "ShardId", "DistroId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerAssistActivityInfos");

            migrationBuilder.DropColumn(
                name: "NextShowingLevelV1",
                table: "UserH5FriendActivityInfos");
        }
    }
}
