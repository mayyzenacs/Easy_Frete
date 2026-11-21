using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EasyFrete.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Produtos",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PesoRealKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    ComprimentoCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    LarguraCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    AlturaCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LinkDrive = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produtos", x => x.Sku);
                });

            migrationBuilder.CreateTable(
                name: "TarifasMagalu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DescricaoFaixa = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PesoMinKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    PesoMaxKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    De79a199SemDesconto = table.Column<decimal>(type: "TEXT", nullable: false),
                    De79a199ComDesconto97 = table.Column<decimal>(type: "TEXT", nullable: false),
                    De79a199Fulfillment = table.Column<decimal>(type: "TEXT", nullable: false),
                    Acima199SemDesconto = table.Column<decimal>(type: "TEXT", nullable: false),
                    Acima199ComDesconto97 = table.Column<decimal>(type: "TEXT", nullable: false),
                    Acima199Fulfillment = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasMagalu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false, collation: "NOCASE"),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Produtos");

            migrationBuilder.DropTable(
                name: "TarifasMagalu");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
