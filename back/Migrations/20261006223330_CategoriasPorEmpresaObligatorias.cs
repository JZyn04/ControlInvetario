using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasPorEmpresaObligatorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La transición usa un respaldo consistente junto al binario anterior para recuperar.
            // Se ejecuta en una sola transacción, sin escritores de la versión por bodega.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE categorias_empresa_antes ON COMMIT DROP AS
                  SELECT c.*, min("Id") OVER (PARTITION BY "EmpresaId","NombreNormalizado") AS destino
                  FROM "CategoriaInventario" c;
                CREATE TEMP TABLE productos_categoria_antes ON COMMIT DROP AS
                  SELECT "Id","EmpresaId","Name","CategoriaId" FROM products;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CategoriaInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "CategoriaInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_BodegaId_CategoriaId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_BodegaId_CategoriaId",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_BodegaId_Id",
                table: "CategoriaInventario");

            migrationBuilder.DropIndex(
                name: "IX_CategoriaInventario_EmpresaId_BodegaId_NombreNormalizado",
                table: "CategoriaInventario");

            migrationBuilder.DropColumn(
                name: "BodegaId",
                table: "CategoriaInventario");

            migrationBuilder.Sql("""
                UPDATE products p SET "CategoriaId"=c.destino, "UpdatedAtUtc"=now()
                  FROM categorias_empresa_antes c
                  WHERE p."EmpresaId"=c."EmpresaId" AND p."CategoriaId"=c."Id" AND c."Id"<>c.destino;
                INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
                  SELECT "EmpresaId",'Sistema','Eliminado','Categoria',"Id"::text,
                    left('Unificada categoría de bodega #'||"BodegaId"||' · '||"Nombre"||' en categoría empresarial #'||destino,600),now()
                  FROM categorias_empresa_antes WHERE "Id"<>destino;
                DELETE FROM "CategoriaInventario" c USING categorias_empresa_antes a
                  WHERE c."EmpresaId"=a."EmpresaId" AND c."Id"=a."Id" AND a."Id"<>a.destino;
                INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
                  SELECT "EmpresaId",'Sistema','Editado','Categoria',"Id"::text,
                    left("Nombre"||' · Categoría ahora disponible en toda la empresa; antes bodega #'||"BodegaId",600),now()
                  FROM categorias_empresa_antes WHERE "Id"=destino;

                -- Únicamente empresas que YA tienen productos sin categoría; no es un catálogo automático del registro.
                WITH nuevas AS (
                  INSERT INTO "CategoriaInventario" ("EmpresaId","Nombre","NombreNormalizado","Activa")
                    SELECT DISTINCT p."EmpresaId",'Sin clasificar','SIN CLASIFICAR',true FROM products p
                    WHERE p."CategoriaId" IS NULL AND NOT EXISTS (
                      SELECT 1 FROM "CategoriaInventario" c WHERE c."EmpresaId"=p."EmpresaId" AND c."NombreNormalizado"='SIN CLASIFICAR')
                    RETURNING "Id","EmpresaId"
                )
                INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
                  SELECT "EmpresaId",'Sistema','Creado','Categoria',"Id"::text,'Sin clasificar · Migración de productos existentes sin categoría',now() FROM nuevas;
                UPDATE products p SET "CategoriaId"=c."Id", "UpdatedAtUtc"=now()
                  FROM "CategoriaInventario" c WHERE p."CategoriaId" IS NULL AND c."EmpresaId"=p."EmpresaId" AND c."NombreNormalizado"='SIN CLASIFICAR';
                INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
                  SELECT p."EmpresaId",'Sistema','Editado','Producto',p."Id"::text,
                    left(p."Name"||' · Categoría: '||COALESCE(a."CategoriaId"::text,'sin categoría')||' → '||p."CategoriaId",600),now()
                  FROM products p JOIN productos_categoria_antes a ON p."EmpresaId"=a."EmpresaId" AND p."Id"=a."Id"
                  WHERE p."CategoriaId" IS DISTINCT FROM a."CategoriaId";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CategoriaId",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_Id",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaInventario_EmpresaId_NombreNormalizado",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" },
                principalTable: "CategoriaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  RAISE EXCEPTION 'Rollback cancelado: las categorías empresariales requieren el respaldo y binario anteriores; no se vuelve a separarlas automáticamente por bodega.';
                END $$;
                """);
        }
    }
}
