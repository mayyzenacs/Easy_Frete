using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyFrete.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAmazonTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesAmazon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    UsaPesoCubado = table.Column<bool>(type: "INTEGER", nullable: false),
                    LimitePrecoTabelaPeso = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    LimitePesoTabelaKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    RegiaoReferenciaDba = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TaxaFixaObrigatoria = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesAmazon", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TarifasAmazon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Modalidade = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TipoRegra = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    DescricaoFaixaPeso = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PrecoMin = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    PrecoMax = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    PesoMinKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    PesoMaxKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    Tarifa = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    FreteCliente = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasAmazon", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TarifasAmazon_Modalidade_TipoRegra_PrecoMin_PrecoMax_PesoMaxKg",
                table: "TarifasAmazon",
                columns: new[] { "Modalidade", "TipoRegra", "PrecoMin", "PrecoMax", "PesoMaxKg" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesAmazon");

            migrationBuilder.DropTable(
                name: "TarifasAmazon");
        }
    }
}
