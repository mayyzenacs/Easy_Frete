using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyFrete.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDeletionNotificationsAndPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Usuarios",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "legacy-stamp");

            migrationBuilder.CreateTable(
                name: "ControlesEnvioRedefinicao",
                columns: table => new
                {
                    EmailHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TentativasNaJanela = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimeiraTentativaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaTentativaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BloqueadoAteEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlesEnvioRedefinicao", x => x.EmailHash);
                });

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Lida = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LidaEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Usuarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitacoesExclusao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioAlvoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UsuarioAlvoNome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    UsuarioAlvoEmail = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    SolicitadoPorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SolicitadoPorNome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecididoPorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecididoPorNome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ObservacaoDecisao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DecididoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacoesExclusao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokensRedefinicaoSenha",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsadoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevogadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRedefinicaoSenha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRedefinicaoSenha_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_DestinatarioId_Lida_CriadoEm",
                table: "Notificacoes",
                columns: new[] { "DestinatarioId", "Lida", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesExclusao_SolicitadoPorId_CriadoEm",
                table: "SolicitacoesExclusao",
                columns: new[] { "SolicitadoPorId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesExclusao_UsuarioAlvoId_Status",
                table: "SolicitacoesExclusao",
                columns: new[] { "UsuarioAlvoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TokensRedefinicaoSenha_TokenHash",
                table: "TokensRedefinicaoSenha",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensRedefinicaoSenha_UsuarioId_CriadoEm",
                table: "TokensRedefinicaoSenha",
                columns: new[] { "UsuarioId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlesEnvioRedefinicao");

            migrationBuilder.DropTable(
                name: "Notificacoes");

            migrationBuilder.DropTable(
                name: "SolicitacoesExclusao");

            migrationBuilder.DropTable(
                name: "TokensRedefinicaoSenha");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Usuarios");
        }
    }
}
