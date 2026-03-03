using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class 去掉貔貅的activityID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "ActivityPiggyBanks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityId",
                table: "ActivityPiggyBanks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
