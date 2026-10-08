using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class MovimientosKardexPrestamos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "products",
                type: "numeric(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumStock",
                table: "products",
                type: "numeric(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "AllowsFractions",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unidad");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "products",
                type: "bigint",
                rowVersion: true,
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_products_EmpresaId_Id",
                table: "products",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateTable(
                name: "PrestamoInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    ProductoId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    Devuelta = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    DestinatarioUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Destinatario = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EsExterno = table.Column<bool>(type: "boolean", nullable: false),
                    FechaPrevista = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrestamoInventario", x => x.Id);
                    table.UniqueConstraint("AK_PrestamoInventario_EmpresaId_ProductoId_Id", x => new { x.EmpresaId, x.ProductoId, x.Id });
                    table.CheckConstraint("CK_PrestamoInventario_Cantidades", "\"Cantidad\" > 0 AND \"Devuelta\" >= 0 AND \"Devuelta\" <= \"Cantidad\"");
                    table.CheckConstraint("CK_PrestamoInventario_Destinatario", "length(trim(\"Destinatario\")) > 0 AND (NOT \"EsExterno\" OR \"DestinatarioUsuarioId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PrestamoInventario_Usuario_EmpresaId_DestinatarioUsuarioId",
                        columns: x => new { x.EmpresaId, x.DestinatarioUsuarioId },
                        principalTable: "Usuario",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrestamoInventario_products_EmpresaId_ProductoId",
                        columns: x => new { x.EmpresaId, x.ProductoId },
                        principalTable: "products",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoInventario",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    ProductoId = table.Column<int>(type: "integer", nullable: false),
                    PrestamoId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unidad = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    SaldoAnterior = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    Cambio = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    SaldoPosterior = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cliente = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(24,2)", precision: 24, scale: 2, nullable: true),
                    AutorId = table.Column<int>(type: "integer", nullable: true),
                    CorreoAutor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: true),
                    SolicitudDatos = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoInventario", x => x.Id);
                    table.CheckConstraint("CK_MovimientoInventario_Saldos", "\"SaldoAnterior\" >= 0 AND \"SaldoPosterior\" >= 0 AND \"SaldoAnterior\" + \"Cambio\" = \"SaldoPosterior\" AND \"Cantidad\" >= 0");
                    table.CheckConstraint("CK_MovimientoInventario_Tipo", "\"Tipo\" IN ('Inicial', 'Entrada', 'Venta', 'Consumo', 'Prestamo', 'DevolucionPrestamo', 'Devolucion', 'Conteo', 'Ajuste')");
                    table.ForeignKey(
                        name: "FK_MovimientoInventario_PrestamoInventario_EmpresaId_ProductoI~",
                        columns: x => new { x.EmpresaId, x.ProductoId, x.PrestamoId },
                        principalTable: "PrestamoInventario",
                        principalColumns: new[] { "EmpresaId", "ProductoId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoInventario_products_EmpresaId_ProductoId",
                        columns: x => new { x.EmpresaId, x.ProductoId },
                        principalTable: "products",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_Cantidades",
                table: "products",
                sql: "\"Quantity\" >= 0 AND \"MinimumStock\" >= 0 AND (\"AllowsFractions\" OR (trunc(\"Quantity\") = \"Quantity\" AND trunc(\"MinimumStock\") = \"MinimumStock\"))");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_ProductoId_Id",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_ProductoId_PrestamoId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "PrestamoId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_SolicitudId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "SolicitudId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrestamoInventario_EmpresaId_DestinatarioUsuarioId",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "DestinatarioUsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrestamoInventario_EmpresaId_ProductoId_Id",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "Id" });

            migrationBuilder.Sql("""
                INSERT INTO "MovimientoInventario"
                    ("EmpresaId", "ProductoId", "Tipo", "Unidad", "Cantidad", "SaldoAnterior", "Cambio", "SaldoPosterior", "Motivo", "CorreoAutor", "FechaUtc")
                SELECT "EmpresaId", "Id", 'Inicial', "Unit", "Quantity", 0, "Quantity", "Quantity",
                    'Saldo existente al incorporar el kárdex', 'Sistema', now() FROM products;
                -- No aumenta permisos de los roles personalizados.
                UPDATE "Rol" SET "Permisos" = 255 WHERE "CodigoSistema" IN ('AdministradorEmpresa', 'OperadorInventario');

                CREATE FUNCTION inventario_version() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."Version" := OLD."Version" + 1;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inventario_version BEFORE UPDATE ON products
                    FOR EACH ROW EXECUTE FUNCTION inventario_version();

                -- Único punto de registro del saldo: también captura escrituras directas antiguas.
                -- La migración decimal requiere desplegar el backend nuevo antes de reabrir el servicio.
                CREATE FUNCTION inventario_kardex() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER inventario_kardex AFTER INSERT OR UPDATE ON products
                    FOR EACH ROW EXECUTE FUNCTION inventario_kardex();

                CREATE FUNCTION inventario_historial_inmutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'El kárdex no se edita ni se borra; registrá un movimiento de corrección';
                END $$;
                CREATE TRIGGER inventario_historial_inmutable BEFORE UPDATE OR DELETE ON "MovimientoInventario"
                    FOR EACH ROW EXECUTE FUNCTION inventario_historial_inmutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Evita truncar cantidades o perder operaciones al volver a una versión anterior.
            // Una vez usado el módulo, restaurar un respaldo es la vía de rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "PrestamoInventario") OR
                        EXISTS (SELECT 1 FROM "MovimientoInventario" WHERE "Tipo" <> 'Inicial' OR "AutorId" IS NOT NULL OR
                            "SolicitudId" IS NOT NULL OR "Motivo" <> 'Saldo existente al incorporar el kárdex') OR
                        EXISTS (SELECT 1 FROM products WHERE NOT "IsActive" OR "Unit" <> 'Unidad' OR "AllowsFractions" OR
                            "Quantity" > 2147483647 OR "MinimumStock" > 2147483647 OR
                            trunc("Quantity") <> "Quantity" OR trunc("MinimumStock") <> "MinimumStock") THEN
                        RAISE EXCEPTION 'Rollback cancelado: hay datos de inventario nuevos. Restaurá un respaldo, no trunques cantidades ni historial.';
                    END IF;
                END $$;
                DROP TRIGGER inventario_kardex ON products;
                DROP FUNCTION inventario_kardex();
                DROP TRIGGER inventario_version ON products;
                DROP FUNCTION inventario_version();
                DROP TRIGGER inventario_historial_inmutable ON "MovimientoInventario";
                DROP FUNCTION inventario_historial_inmutable();
                UPDATE "Rol" SET "Permisos" = 15 WHERE "CodigoSistema" IN ('AdministradorEmpresa', 'OperadorInventario');
                """);
            migrationBuilder.DropTable(
                name: "MovimientoInventario");

            migrationBuilder.DropTable(
                name: "PrestamoInventario");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_products_EmpresaId_Id",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_Cantidades",
                table: "products");

            migrationBuilder.DropColumn(
                name: "AllowsFractions",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "products");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,3)",
                oldPrecision: 15,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "MinimumStock",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,3)",
                oldPrecision: 15,
                oldScale: 3);
        }
    }
}
