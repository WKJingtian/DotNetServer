using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class modifyAssistActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerAssistActivityInfos",
                table: "PlayerAssistActivityInfos");

            migrationBuilder.DropIndex(
                name: "IX_PlayerAssistActivityInfos_PlayerId_ShardId_DistroId",
                table: "PlayerAssistActivityInfos");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PlayerAssistActivityInfos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerAssistActivityInfos",
                table: "PlayerAssistActivityInfos",
                columns: new[] { "PlayerId", "ShardId", "DistroId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerAssistActivityInfos",
                table: "PlayerAssistActivityInfos");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PlayerAssistActivityInfos",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerAssistActivityInfos",
                table: "PlayerAssistActivityInfos",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAssistActivityInfos_PlayerId_ShardId_DistroId",
                table: "PlayerAssistActivityInfos",
                columns: new[] { "PlayerId", "ShardId", "DistroId" },
                unique: true);
        }
    }
}
