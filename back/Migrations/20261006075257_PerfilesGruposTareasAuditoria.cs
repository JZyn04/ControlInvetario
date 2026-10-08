using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class PerfilesGruposTareasAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Usuario",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PermisosGrupos",
                table: "Rol",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PermisosTareas",
                table: "Rol",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Usuario_EmpresaId_Id",
                table: "Usuario",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateTable(
                name: "GrupoTrabajo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupervisorId = table.Column<int>(type: "integer", nullable: true),
                    CreadoEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoTrabajo", x => x.Id);
                    table.UniqueConstraint("AK_GrupoTrabajo_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_GrupoTrabajo_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrupoTrabajo_Usuario_EmpresaId_SupervisorId",
                        columns: x => new { x.EmpresaId, x.SupervisorId },
                        principalTable: "Usuario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistroAuditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: true),
                    CorreoAutor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Accion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntidadId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistroAuditoria_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EstadoTarea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoTarea", x => x.Id);
                    table.UniqueConstraint("AK_EstadoTarea_EmpresaId_GrupoId_Id", x => new { x.EmpresaId, x.GrupoId, x.Id });
                    table.ForeignKey(
                        name: "FK_EstadoTarea_GrupoTrabajo_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "GrupoTrabajo",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MiembroGrupo",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembroGrupo", x => new { x.EmpresaId, x.GrupoId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_MiembroGrupo_GrupoTrabajo_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "GrupoTrabajo",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiembroGrupo_Usuario_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tarea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    EstadoId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AsignadoAId = table.Column<int>(type: "integer", nullable: true),
                    PermitirAutoasignacion = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreadaEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadaEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarea", x => x.Id);
                    table.UniqueConstraint("AK_Tarea_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Tarea_Autoasignacion", "\"AsignadoAId\" IS NULL OR NOT \"PermitirAutoasignacion\"");
                    table.ForeignKey(
                        name: "FK_Tarea_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tarea_EstadoTarea_EmpresaId_GrupoId_EstadoId",
                        columns: x => new { x.EmpresaId, x.GrupoId, x.EstadoId },
                        principalTable: "EstadoTarea",
                        principalColumns: new[] { "EmpresaId", "GrupoId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarea_GrupoTrabajo_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "GrupoTrabajo",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarea_Usuario_EmpresaId_AsignadoAId",
                        columns: x => new { x.EmpresaId, x.AsignadoAId },
                        principalTable: "Usuario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotaTarea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    TareaId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: true),
                    CorreoAutor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TipoAutor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreadaEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadaEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaTarea", x => x.Id);
                    table.CheckConstraint("CK_NotaTarea_TipoAutor", "\"TipoAutor\" IN ('Asignado', 'Supervisor')");
                    table.ForeignKey(
                        name: "FK_NotaTarea_Tarea_EmpresaId_TareaId",
                        columns: x => new { x.EmpresaId, x.TareaId },
                        principalTable: "Tarea",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotaTarea_Usuario_EmpresaId_AutorId",
                        columns: x => new { x.EmpresaId, x.AutorId },
                        principalTable: "Usuario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstadoTarea_EmpresaId_GrupoId_NombreNormalizado",
                table: "EstadoTarea",
                columns: new[] { "EmpresaId", "GrupoId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrupoTrabajo_EmpresaId_NombreNormalizado",
                table: "GrupoTrabajo",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrupoTrabajo_EmpresaId_SupervisorId",
                table: "GrupoTrabajo",
                columns: new[] { "EmpresaId", "SupervisorId" });

            migrationBuilder.CreateIndex(
                name: "IX_MiembroGrupo_EmpresaId_UsuarioId",
                table: "MiembroGrupo",
                columns: new[] { "EmpresaId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotaTarea_EmpresaId_AutorId",
                table: "NotaTarea",
                columns: new[] { "EmpresaId", "AutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotaTarea_EmpresaId_TareaId",
                table: "NotaTarea",
                columns: new[] { "EmpresaId", "TareaId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_EmpresaId_Id",
                table: "RegistroAuditoria",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarea_EmpresaId_AsignadoAId",
                table: "Tarea",
                columns: new[] { "EmpresaId", "AsignadoAId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarea_EmpresaId_GrupoId_EstadoId",
                table: "Tarea",
                columns: new[] { "EmpresaId", "GrupoId", "EstadoId" });

            // Amplía las empresas existentes sin tocar sus cuentas, productos o roles propios.
            // Si ya existe un nombre, el rol de sistema recibe un sufijo, no lo reemplaza.
            migrationBuilder.Sql("""
                UPDATE "Rol" SET "PermisosTareas" = 31, "PermisosGrupos" = 3
                WHERE "CodigoSistema" = 'AdministradorEmpresa';
                WITH nuevos AS (
                    INSERT INTO "Rol" ("EmpresaId", "Nombre", "NombreNormalizado", "CodigoSistema", "Permisos", "PermisosTareas", "PermisosGrupos")
                    SELECT e."Id", nombre.valor, upper(nombre.valor), sistema.codigo, 0, sistema.tareas, sistema.grupos
                    FROM "Empresa" e
                    CROSS JOIN (VALUES ('SupervisorTareas', 'Supervisor de tareas', 31, 1),
                        ('ColaboradorTareas', 'Colaborador de tareas', 29, 1),
                        ('GestorGrupos', 'Gestor de grupos', 0, 3)) sistema(codigo, base, tareas, grupos)
                    CROSS JOIN LATERAL (
                        SELECT CASE WHEN n = 0 THEN sistema.base ELSE sistema.base || ' (sistema ' || n || ')' END AS valor
                        FROM generate_series(0, (SELECT count(*)::integer + 1 FROM "Rol" r WHERE r."EmpresaId" = e."Id")) n
                        WHERE NOT EXISTS (SELECT 1 FROM "Rol" r WHERE r."EmpresaId" = e."Id" AND r."NombreNormalizado" =
                            upper(CASE WHEN n = 0 THEN sistema.base ELSE sistema.base || ' (sistema ' || n || ')' END))
                        ORDER BY n LIMIT 1
                    ) nombre
                    WHERE NOT EXISTS (SELECT 1 FROM "Rol" r WHERE r."EmpresaId" = e."Id" AND r."CodigoSistema" = sistema.codigo)
                    RETURNING "Id", "EmpresaId", "Nombre"
                )
                INSERT INTO "RegistroAuditoria" ("EmpresaId", "CorreoAutor", "FechaUtc", "Accion", "Entidad", "EntidadId", "Detalle")
                SELECT "EmpresaId", 'Sistema', now(), 'Creado', 'Rol', "Id"::text, "Nombre" FROM nuevos;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El rollback pierde los datos del módulo nuevo, pero conserva las cuentas.
            migrationBuilder.Sql("""
                UPDATE "Usuario" u SET "RolId" = operador."Id", "EsPrincipal" = FALSE
                FROM "Rol" nuevo, "Rol" operador
                WHERE u."EmpresaId" = nuevo."EmpresaId" AND u."RolId" = nuevo."Id"
                    AND nuevo."CodigoSistema" IN ('SupervisorTareas', 'ColaboradorTareas', 'GestorGrupos')
                    AND operador."EmpresaId" = u."EmpresaId" AND operador."CodigoSistema" = 'OperadorInventario';
                DELETE FROM "Rol" WHERE "CodigoSistema" IN ('SupervisorTareas', 'ColaboradorTareas', 'GestorGrupos');
                """);
            migrationBuilder.DropTable(
                name: "MiembroGrupo");

            migrationBuilder.DropTable(
                name: "NotaTarea");

            migrationBuilder.DropTable(
                name: "RegistroAuditoria");

            migrationBuilder.DropTable(
                name: "Tarea");

            migrationBuilder.DropTable(
                name: "EstadoTarea");

            migrationBuilder.DropTable(
                name: "GrupoTrabajo");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Usuario_EmpresaId_Id",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "PermisosGrupos",
                table: "Rol");

            migrationBuilder.DropColumn(
                name: "PermisosTareas",
                table: "Rol");
        }
    }
}
