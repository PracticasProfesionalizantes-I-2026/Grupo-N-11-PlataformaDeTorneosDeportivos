using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipoCapitanForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Equipos_CapitanId",
                table: "Equipos",
                column: "CapitanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipos_Usuarios_CapitanId",
                table: "Equipos",
                column: "CapitanId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipos_Usuarios_CapitanId",
                table: "Equipos");

            migrationBuilder.DropIndex(
                name: "IX_Equipos_CapitanId",
                table: "Equipos");
        }
    }
}
