using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class NewRestrictionRuleOnOneShotKill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChallengeVictoryCount",
                table: "ActivityOneShotKills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ChallengeVictoryUpdateTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "NormalVictoryCount",
                table: "ActivityOneShotKills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "NormalVictoryUpdateTimestamp",
                table: "ActivityOneShotKills",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChallengeVictoryCount",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "ChallengeVictoryUpdateTimestamp",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "NormalVictoryCount",
                table: "ActivityOneShotKills");

            migrationBuilder.DropColumn(
                name: "NormalVictoryUpdateTimestamp",
                table: "ActivityOneShotKills");
        }
    }
}
