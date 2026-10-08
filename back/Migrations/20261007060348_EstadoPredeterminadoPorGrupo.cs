using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class EstadoPredeterminadoPorGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPredeterminado",
                table: "EstadoTarea",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Conserva estados y tareas; solo añade una preferencia inicial por grupo existente.
            migrationBuilder.Sql("""
                WITH elegidos AS (
                  SELECT "Id", row_number() OVER (PARTITION BY "EmpresaId", "GrupoId"
                    ORDER BY ("NombreNormalizado" = 'PENDIENTE') DESC, "Orden", "Id") AS posicion
                  FROM "EstadoTarea"
                )
                UPDATE "EstadoTarea" e SET "EsPredeterminado" = true
                  FROM elegidos s WHERE e."Id" = s."Id" AND s.posicion = 1;
                INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
                  SELECT "EmpresaId",'Sistema','Editado','Estado',"Id"::text,
                    left("Nombre" || ' · Estado predeterminado inicial del grupo #' || "GrupoId",600),now()
                  FROM "EstadoTarea" WHERE "EsPredeterminado";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EstadoTarea_EmpresaId_GrupoId",
                table: "EstadoTarea",
                columns: new[] { "EmpresaId", "GrupoId" },
                unique: true,
                filter: "\"EsPredeterminado\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "EstadoTarea") THEN
                    RAISE EXCEPTION 'Rollback cancelado: conservá la columna adicional con el binario anterior o recuperá el respaldo; no se descartan las preferencias de grupos usados.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_EstadoTarea_EmpresaId_GrupoId",
                table: "EstadoTarea");

            migrationBuilder.DropColumn(
                name: "EsPredeterminado",
                table: "EstadoTarea");
        }
    }
}
