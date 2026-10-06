using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class RolesPorEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RolId",
                table: "Usuario",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CodigoSistema = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Permisos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                    table.UniqueConstraint("AK_Rol_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Rol_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Conserva empresas, usuarios, contraseñas y productos de la versión anterior.
            migrationBuilder.Sql("""
                INSERT INTO "Rol" ("EmpresaId", "Nombre", "NombreNormalizado", "CodigoSistema", "Permisos")
                SELECT e."Id", v.nombre, v.normalizado, v.codigo, 15 FROM "Empresa" e
                CROSS JOIN (VALUES
                  ('Administrador de empresa', 'ADMINISTRADOR DE EMPRESA', 'AdministradorEmpresa'),
                  ('Operador de inventario', 'OPERADOR DE INVENTARIO', 'OperadorInventario')
                ) AS v(nombre, normalizado, codigo);

                UPDATE "Usuario" u SET "RolId" = r."Id"
                FROM "Rol" r WHERE r."EmpresaId" = u."EmpresaId" AND r."CodigoSistema" =
                  CASE WHEN u."EsPrincipal" THEN 'AdministradorEmpresa' ELSE 'OperadorInventario' END;

                -- Si una empresa importada no tenía principal, conserva acceso mediante su primer usuario.
                UPDATE "Usuario" u SET "RolId" = r."Id", "EsPrincipal" = TRUE
                FROM "Rol" r WHERE r."EmpresaId" = u."EmpresaId" AND r."CodigoSistema" = 'AdministradorEmpresa'
                  AND u."Id" = (SELECT MIN(x."Id") FROM "Usuario" x WHERE x."EmpresaId" = u."EmpresaId")
                  AND NOT EXISTS (SELECT 1 FROM "Usuario" x WHERE x."EmpresaId" = u."EmpresaId" AND x."EsPrincipal");
                """);
            migrationBuilder.AlterColumn<int>(name: "RolId", table: "Usuario", type: "integer", nullable: false,
                oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_EmpresaId_RolId",
                table: "Usuario",
                columns: new[] { "EmpresaId", "RolId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rol_EmpresaId_CodigoSistema",
                table: "Rol",
                columns: new[] { "EmpresaId", "CodigoSistema" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rol_EmpresaId_NombreNormalizado",
                table: "Rol",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuario_Rol_EmpresaId_RolId",
                table: "Usuario",
                columns: new[] { "EmpresaId", "RolId" },
                principalTable: "Rol",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuario_Rol_EmpresaId_RolId",
                table: "Usuario");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropIndex(
                name: "IX_Usuario_EmpresaId_RolId",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "RolId",
                table: "Usuario");
        }
    }
}
