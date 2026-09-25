using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditosApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditosEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    IngresosMensuales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.CheckConstraint("CK_Clientes_IngresosMensuales_Positivo", "IngresosMensuales > 0");
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    MontoSolicitado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesCredito", x => x.Id);
                    table.CheckConstraint("CK_Solicitudes_Monto_Positivo", "MontoSolicitado > 0");
                    table.ForeignKey(
                        name: "FK_SolicitudesCredito_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_UsuarioId",
                table: "Clientes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesCredito_ClienteId",
                table: "SolicitudesCredito",
                column: "ClienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesCredito");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
