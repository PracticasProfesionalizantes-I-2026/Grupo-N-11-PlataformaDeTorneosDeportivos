using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaPosiciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TablaPosiciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TorneoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EquipoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EquipoNombre = table.Column<string>(type: "TEXT", nullable: false),
                    Posicion = table.Column<int>(type: "INTEGER", nullable: false),
                    PartidosJugados = table.Column<int>(type: "INTEGER", nullable: false),
                    PartidosGanados = table.Column<int>(type: "INTEGER", nullable: false),
                    PartidosEmpatados = table.Column<int>(type: "INTEGER", nullable: false),
                    PartidosPerdidos = table.Column<int>(type: "INTEGER", nullable: false),
                    GolesAFavor = table.Column<int>(type: "INTEGER", nullable: false),
                    GolesEnContra = table.Column<int>(type: "INTEGER", nullable: false),
                    DiferenciaGoles = table.Column<int>(type: "INTEGER", nullable: false),
                    Puntos = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TablaPosiciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TablaPosiciones_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TablaPosiciones_Torneos_TorneoId",
                        column: x => x.TorneoId,
                        principalTable: "Torneos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TablaPosiciones_EquipoId",
                table: "TablaPosiciones",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_TablaPosiciones_TorneoId_EquipoId",
                table: "TablaPosiciones",
                columns: new[] { "TorneoId", "EquipoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TablaPosiciones");
        }
    }
}
