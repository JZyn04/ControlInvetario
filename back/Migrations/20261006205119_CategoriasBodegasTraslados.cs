using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasBodegasTraslados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_Tipo",
                table: "MovimientoInventario");

            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodegaOrigenId",
                table: "PrestamoInventario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BodegaId",
                table: "MovimientoInventario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CambioBodega",
                table: "MovimientoInventario",
                type: "numeric(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoBodegaAnterior",
                table: "MovimientoInventario",
                type: "numeric(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoBodegaPosterior",
                table: "MovimientoInventario",
                type: "numeric(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TrasladoId",
                table: "MovimientoInventario",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BodegaInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    Predeterminada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodegaInventario", x => x.Id);
                    table.UniqueConstraint("AK_BodegaInventario_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_BodegaInventario_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoriaInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaInventario", x => x.Id);
                    table.UniqueConstraint("AK_CategoriaInventario_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_CategoriaInventario_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExistenciaBodega",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    ProductoId = table.Column<int>(type: "integer", nullable: false),
                    BodegaId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExistenciaBodega", x => new { x.EmpresaId, x.ProductoId, x.BodegaId });
                    table.CheckConstraint("CK_ExistenciaBodega_Cantidad", "\"Cantidad\" >= 0");
                    table.ForeignKey(
                        name: "FK_ExistenciaBodega_BodegaInventario_EmpresaId_BodegaId",
                        columns: x => new { x.EmpresaId, x.BodegaId },
                        principalTable: "BodegaInventario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExistenciaBodega_products_EmpresaId_ProductoId",
                        columns: x => new { x.EmpresaId, x.ProductoId },
                        principalTable: "products",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Todo saldo e historial anterior pertenece inicialmente a la bodega principal.
            // La actualización histórica ocurre solo durante esta migración y en su transacción.
            migrationBuilder.Sql("""
                INSERT INTO "BodegaInventario" ("EmpresaId", "Nombre", "NombreNormalizado", "Activa", "Predeterminada")
                SELECT "Id", 'Principal', 'PRINCIPAL', true, true FROM "Empresa";
                INSERT INTO "ExistenciaBodega" ("EmpresaId", "ProductoId", "BodegaId", "Cantidad")
                SELECT p."EmpresaId", p."Id", b."Id", p."Quantity" FROM products p
                JOIN "BodegaInventario" b ON b."EmpresaId" = p."EmpresaId" AND b."Predeterminada";
                DROP TRIGGER inventario_historial_inmutable ON "MovimientoInventario";
                UPDATE "MovimientoInventario" m SET "BodegaId" = b."Id", "SaldoBodegaAnterior" = m."SaldoAnterior",
                    "CambioBodega" = m."Cambio", "SaldoBodegaPosterior" = m."SaldoPosterior"
                FROM "BodegaInventario" b WHERE b."EmpresaId" = m."EmpresaId" AND b."Predeterminada";
                CREATE TRIGGER inventario_historial_inmutable BEFORE UPDATE OR DELETE ON "MovimientoInventario"
                    FOR EACH ROW EXECUTE FUNCTION inventario_historial_inmutable();
                UPDATE "PrestamoInventario" p SET "BodegaOrigenId" = b."Id" FROM "BodegaInventario" b
                    WHERE b."EmpresaId" = p."EmpresaId" AND b."Predeterminada";
                UPDATE "Rol" SET "Permisos" = 4095 WHERE "CodigoSistema" IN ('AdministradorEmpresa', 'OperadorInventario');

                CREATE FUNCTION inventario_bodega_principal() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "BodegaInventario" ("EmpresaId", "Nombre", "NombreNormalizado", "Activa", "Predeterminada")
                    VALUES (NEW."Id", 'Principal', 'PRINCIPAL', true, true);
                    RETURN NULL;
                END $$;
                CREATE TRIGGER inventario_bodega_principal AFTER INSERT ON "Empresa"
                    FOR EACH ROW EXECUTE FUNCTION inventario_bodega_principal();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrestamoInventario_EmpresaId_BodegaOrigenId",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "BodegaOrigenId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "BodegaId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_Bodega",
                table: "MovimientoInventario",
                sql: "\"SaldoBodegaAnterior\" >= 0 AND \"SaldoBodegaPosterior\" >= 0 AND \"SaldoBodegaAnterior\" + \"CambioBodega\" = \"SaldoBodegaPosterior\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_Tipo",
                table: "MovimientoInventario",
                sql: "\"Tipo\" IN ('Inicial', 'Entrada', 'Venta', 'Consumo', 'Prestamo', 'DevolucionPrestamo', 'Devolucion', 'Conteo', 'Ajuste', 'TrasladoSalida', 'TrasladoEntrada')");

            migrationBuilder.CreateIndex(
                name: "IX_BodegaInventario_EmpresaId",
                table: "BodegaInventario",
                column: "EmpresaId",
                unique: true,
                filter: "\"Predeterminada\"");

            migrationBuilder.CreateIndex(
                name: "IX_BodegaInventario_EmpresaId_NombreNormalizado",
                table: "BodegaInventario",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaInventario_EmpresaId_NombreNormalizado",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExistenciaBodega_EmpresaId_BodegaId",
                table: "ExistenciaBodega",
                columns: new[] { "EmpresaId", "BodegaId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "BodegaId" },
                principalTable: "BodegaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrestamoInventario_BodegaInventario_EmpresaId_BodegaOrigenId",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "BodegaOrigenId" },
                principalTable: "BodegaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" },
                principalTable: "CategoriaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION inventario_kardex() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    contexto jsonb := COALESCE(NULLIF(current_setting('inventario.movimiento', true), ''), '{}')::jsonb;
                    previo numeric := 0;
                    clase text;
                    autor integer := NULL;
                    correo text := 'Sistema';
                    movimiento_id bigint;
                    precio numeric;
                    cantidad numeric;
                    bodega integer;
                    anterior_bodega numeric;
                    cambio_bodega numeric;
                    posterior_bodega numeric;
                BEGIN
                    clase := COALESCE(contexto->>'Tipo', CASE WHEN TG_OP = 'INSERT' THEN 'Inicial' ELSE 'Ajuste' END);
                    IF TG_OP = 'UPDATE' THEN
                        previo := OLD."Quantity";
                        IF NEW."Quantity" = previo AND clase NOT IN ('Conteo', 'TrasladoSalida', 'TrasladoEntrada') THEN RETURN NULL; END IF;
                    END IF;
                    cantidad := COALESCE((contexto->>'Cantidad')::numeric, abs(NEW."Quantity" - previo));
                    SELECT "Id" INTO bodega FROM "BodegaInventario" WHERE "EmpresaId" = NEW."EmpresaId" AND "Activa" AND
                        (CASE WHEN contexto->>'BodegaId' IS NULL THEN "Predeterminada" ELSE "Id" = (contexto->>'BodegaId')::integer END);
                    IF bodega IS NULL THEN RAISE EXCEPTION 'Bodega inválida o ajena a la empresa'; END IF;
                    INSERT INTO "ExistenciaBodega" ("EmpresaId", "ProductoId", "BodegaId", "Cantidad")
                        VALUES (NEW."EmpresaId", NEW."Id", bodega, 0) ON CONFLICT DO NOTHING;
                    SELECT "Cantidad" INTO anterior_bodega FROM "ExistenciaBodega"
                        WHERE "EmpresaId" = NEW."EmpresaId" AND "ProductoId" = NEW."Id" AND "BodegaId" = bodega FOR UPDATE;
                    cambio_bodega := CASE WHEN clase = 'TrasladoSalida' THEN -cantidad WHEN clase = 'TrasladoEntrada' THEN cantidad
                        ELSE NEW."Quantity" - previo END;
                    IF clase IN ('TrasladoSalida', 'TrasladoEntrada') AND
                        (NEW."Quantity" <> previo OR cantidad <= 0 OR contexto->>'TrasladoId' IS NULL) THEN
                        RAISE EXCEPTION 'Traslado inválido';
                    END IF;
                    posterior_bodega := anterior_bodega + cambio_bodega;
                    IF posterior_bodega < 0 OR (NOT NEW."AllowsFractions" AND trunc(posterior_bodega) <> posterior_bodega) THEN
                        RAISE EXCEPTION 'Existencia insuficiente o incompatible con la unidad de la bodega';
                    END IF;
                    UPDATE "ExistenciaBodega" SET "Cantidad" = posterior_bodega
                        WHERE "EmpresaId" = NEW."EmpresaId" AND "ProductoId" = NEW."Id" AND "BodegaId" = bodega;
                    IF contexto->>'AutorId' IS NOT NULL THEN
                        SELECT "Id", "Correo" INTO autor, correo FROM "Usuario"
                            WHERE "Id" = (contexto->>'AutorId')::integer AND "EmpresaId" = NEW."EmpresaId";
                        IF NOT FOUND THEN RAISE EXCEPTION 'Autor ajeno a la empresa'; END IF;
                    END IF;
                    precio := (contexto->>'PrecioUnitario')::numeric;
                    INSERT INTO "MovimientoInventario"
                        ("EmpresaId", "ProductoId", "PrestamoId", "Tipo", "Unidad", "Cantidad", "SaldoAnterior", "Cambio", "SaldoPosterior",
                         "BodegaId", "SaldoBodegaAnterior", "CambioBodega", "SaldoBodegaPosterior", "TrasladoId",
                         "Motivo", "Referencia", "Cliente", "PrecioUnitario", "Total", "AutorId", "CorreoAutor", "FechaUtc", "SolicitudId", "SolicitudDatos")
                    VALUES (NEW."EmpresaId", NEW."Id", (contexto->>'PrestamoId')::integer, clase, NEW."Unit", cantidad, previo,
                        NEW."Quantity" - previo, NEW."Quantity", bodega, anterior_bodega, cambio_bodega, posterior_bodega, (contexto->>'TrasladoId')::uuid,
                        COALESCE(contexto->>'Motivo', 'Cambio desde una versión anterior o proceso directo'),
                        contexto->>'Referencia', contexto->>'Cliente', precio, round(precio * cantidad, 2), autor, correo, now(),
                        (contexto->>'SolicitudId')::uuid, contexto->>'SolicitudDatos') RETURNING "Id" INTO movimiento_id;
                    INSERT INTO "RegistroAuditoria"
                        ("EmpresaId", "AutorId", "CorreoAutor", "Accion", "Entidad", "EntidadId", "Detalle", "FechaUtc")
                    VALUES (NEW."EmpresaId", autor, correo, 'Creado', 'Movimiento', movimiento_id::text,
                        left(NEW."Name" || ' · ' || clase || ' · Bodega #' || bodega || ': ' || anterior_bodega || ' → ' || posterior_bodega, 600), now());
                    RETURN NULL;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "PrestamoInventario") OR
                        EXISTS (SELECT 1 FROM products WHERE NOT "IsActive" OR "Unit" <> 'Unidad' OR "AllowsFractions" OR
                            "Quantity" > 2147483647 OR "MinimumStock" > 2147483647 OR
                            trunc("Quantity") <> "Quantity" OR trunc("MinimumStock") <> "MinimumStock") OR
                        EXISTS (SELECT 1 FROM "CategoriaInventario") OR
                        EXISTS (SELECT 1 FROM "BodegaInventario" WHERE NOT "Predeterminada" OR NOT "Activa" OR "Nombre" <> 'Principal') OR
                        EXISTS (SELECT 1 FROM "MovimientoInventario" WHERE "TrasladoId" IS NOT NULL OR "AutorId" IS NOT NULL OR
                            "Tipo" <> 'Inicial' OR "SolicitudId" IS NOT NULL OR "Motivo" <> 'Saldo existente al incorporar el kárdex') OR
                        EXISTS (SELECT 1 FROM "Rol" WHERE "CodigoSistema" IS NULL AND ("Permisos" & 3840) <> 0) THEN
                        RAISE EXCEPTION 'Rollback cancelado: hay categorías, bodegas, traslados o permisos nuevos. Restaurá un respaldo.';
                    END IF;
                END $$;
                DROP TRIGGER inventario_bodega_principal ON "Empresa";
                DROP FUNCTION inventario_bodega_principal();
                UPDATE "Rol" SET "Permisos" = 255 WHERE "CodigoSistema" IN ('AdministradorEmpresa', 'OperadorInventario');
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PrestamoInventario_BodegaInventario_EmpresaId_BodegaOrigenId",
                table: "PrestamoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_CategoriaId",
                table: "products");

            migrationBuilder.DropTable(
                name: "CategoriaInventario");

            migrationBuilder.DropTable(
                name: "ExistenciaBodega");

            migrationBuilder.DropTable(
                name: "BodegaInventario");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_CategoriaId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_PrestamoInventario_EmpresaId_BodegaOrigenId",
                table: "PrestamoInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_Bodega",
                table: "MovimientoInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_Tipo",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BodegaOrigenId",
                table: "PrestamoInventario");

            migrationBuilder.DropColumn(
                name: "BodegaId",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "CambioBodega",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "SaldoBodegaAnterior",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "SaldoBodegaPosterior",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "TrasladoId",
                table: "MovimientoInventario");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_Tipo",
                table: "MovimientoInventario",
                sql: "\"Tipo\" IN ('Inicial', 'Entrada', 'Venta', 'Consumo', 'Prestamo', 'DevolucionPrestamo', 'Devolucion', 'Conteo', 'Ajuste')");
            // Restaura el escritor anterior únicamente después del rollback sin uso de bodegas/categorías.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION inventario_kardex() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    contexto jsonb := COALESCE(NULLIF(current_setting('inventario.movimiento', true), ''), '{}')::jsonb;
                    previo numeric := 0;
                    clase text;
                    autor integer := NULL;
                    correo text := 'Sistema';
                    movimiento_id bigint;
                    precio numeric;
                    cantidad numeric;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        previo := OLD."Quantity";
                        IF NEW."Quantity" = previo AND COALESCE(contexto->>'Tipo', '') <> 'Conteo' THEN RETURN NULL; END IF;
                    END IF;
                    clase := COALESCE(contexto->>'Tipo', CASE WHEN TG_OP = 'INSERT' THEN 'Inicial' ELSE 'Ajuste' END);
                    cantidad := COALESCE((contexto->>'Cantidad')::numeric, abs(NEW."Quantity" - previo));
                    IF contexto->>'AutorId' IS NOT NULL THEN
                        SELECT "Id", "Correo" INTO autor, correo FROM "Usuario"
                            WHERE "Id" = (contexto->>'AutorId')::integer AND "EmpresaId" = NEW."EmpresaId";
                        IF NOT FOUND THEN RAISE EXCEPTION 'Autor ajeno a la empresa'; END IF;
                    END IF;
                    precio := (contexto->>'PrecioUnitario')::numeric;
                    INSERT INTO "MovimientoInventario"
                        ("EmpresaId", "ProductoId", "PrestamoId", "Tipo", "Unidad", "Cantidad", "SaldoAnterior", "Cambio", "SaldoPosterior",
                         "Motivo", "Referencia", "Cliente", "PrecioUnitario", "Total", "AutorId", "CorreoAutor", "FechaUtc", "SolicitudId", "SolicitudDatos")
                    VALUES (NEW."EmpresaId", NEW."Id", (contexto->>'PrestamoId')::integer, clase, NEW."Unit", cantidad, previo,
                        NEW."Quantity" - previo, NEW."Quantity", COALESCE(contexto->>'Motivo', 'Cambio desde una versión anterior o proceso directo'),
                        contexto->>'Referencia', contexto->>'Cliente', precio, round(precio * cantidad, 2), autor, correo, now(),
                        (contexto->>'SolicitudId')::uuid, contexto->>'SolicitudDatos') RETURNING "Id" INTO movimiento_id;
                    INSERT INTO "RegistroAuditoria"
                        ("EmpresaId", "AutorId", "CorreoAutor", "Accion", "Entidad", "EntidadId", "Detalle", "FechaUtc")
                    VALUES (NEW."EmpresaId", autor, correo, 'Creado', 'Movimiento', movimiento_id::text,
                        left(NEW."Name" || ' · ' || clase || ' · Disponible: ' || previo || ' → ' || NEW."Quantity", 600), now());
                    RETURN NULL;
                END $$;
                """);
        }
    }
}
