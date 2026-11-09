using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTorneoCampeonId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampeonId",
                table: "Torneos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Torneos_CampeonId",
                table: "Torneos",
                column: "CampeonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Torneos_Equipos_CampeonId",
                table: "Torneos",
                column: "CampeonId",
                principalTable: "Equipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Torneos_Equipos_CampeonId",
                table: "Torneos");

            migrationBuilder.DropIndex(
                name: "IX_Torneos_CampeonId",
                table: "Torneos");

            migrationBuilder.DropColumn(
                name: "CampeonId",
                table: "Torneos");
        }
    }
}
