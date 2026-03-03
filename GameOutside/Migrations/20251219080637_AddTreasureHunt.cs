using System;
using System.Collections.Generic;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasureHunt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityTreasureHunts",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    LastRefreshTime = table.Column<long>(type: "bigint", nullable: false),
                    TodayRefreshCount = table.Column<int>(type: "integer", nullable: false),
                    ScorePoints = table.Column<int>(type: "integer", nullable: false),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    ScoreRewardClaimStatus = table.Column<long>(type: "bigint", nullable: false),
                    RewardSlots = table.Column<List<TreasureHuntSlot>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTreasureHunts", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityTreasureHunts");
        }
    }
}
