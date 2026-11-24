using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyFrete.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracaoMagalu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesMagalu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxaFixaObrigatoria = table.Column<decimal>(type: "TEXT", nullable: false),
                    LimiteTabela = table.Column<decimal>(type: "TEXT", nullable: false),
                    LimiteAcima199 = table.Column<decimal>(type: "TEXT", nullable: false),
                    PercentualAbaixoLimiteFulfillment = table.Column<decimal>(type: "TEXT", nullable: false),
                    PercentualAbaixoLimiteColeta = table.Column<decimal>(type: "TEXT", nullable: false),
                    DivisorCubagem = table.Column<decimal>(type: "TEXT", nullable: false),
                    PercentualReputacaoMinima = table.Column<decimal>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesMagalu", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesMagalu");
        }
    }
}
