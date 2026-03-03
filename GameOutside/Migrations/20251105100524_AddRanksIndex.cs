using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddRanksIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRanks_SeasonNumber_Division_GroupId",
                table: "UserRanks");

            migrationBuilder.DropIndex(
                name: "IX_UserEndlessRanks_SurvivorScore_SurvivorTimestamp",
                table: "UserEndlessRanks");

            migrationBuilder.DropIndex(
                name: "IX_UserEndlessRanks_TowerDefenceScore_TowerDefenceTimestamp",
                table: "UserEndlessRanks");

            migrationBuilder.DropIndex(
                name: "IX_UserEndlessRanks_TrueEndlessScore_TrueEndlessTimestamp",
                table: "UserEndlessRanks");

            migrationBuilder.CreateTable(
                name: "LocalRedisLuaScripts",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sha = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalRedisLuaScripts", x => new { x.Name, x.ShardId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRanks_SeasonNumber_Division_GroupId_ShardId",
                table: "UserRanks",
                columns: new[] { "SeasonNumber", "Division", "GroupId", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRanks_SeasonNumber_ShardId",
                table: "UserRanks",
                columns: new[] { "SeasonNumber", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_SeasonNumber_ShardId",
                table: "UserEndlessRanks",
                columns: new[] { "SeasonNumber", "ShardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalRedisLuaScripts");

            migrationBuilder.DropIndex(
                name: "IX_UserRanks_SeasonNumber_Division_GroupId_ShardId",
                table: "UserRanks");

            migrationBuilder.DropIndex(
                name: "IX_UserRanks_SeasonNumber_ShardId",
                table: "UserRanks");

            migrationBuilder.DropIndex(
                name: "IX_UserEndlessRanks_SeasonNumber_ShardId",
                table: "UserEndlessRanks");

            migrationBuilder.CreateIndex(
                name: "IX_UserRanks_SeasonNumber_Division_GroupId",
                table: "UserRanks",
                columns: new[] { "SeasonNumber", "Division", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_SurvivorScore_SurvivorTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "SurvivorScore", "SurvivorTimestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_TowerDefenceScore_TowerDefenceTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "TowerDefenceScore", "TowerDefenceTimestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_TrueEndlessScore_TrueEndlessTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "TrueEndlessScore", "TrueEndlessTimestamp" },
                descending: new[] { true, false });
        }
    }
}
