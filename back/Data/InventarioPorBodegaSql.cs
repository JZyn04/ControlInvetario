namespace back.Data;

// Solo se ejecuta desde la migración, dentro de su transacción y con bloqueo DDL.
public static class InventarioPorBodegaSql
{
    public const string Preparar = """
        DROP TRIGGER inventario_bodega_principal ON "Empresa";
        DROP FUNCTION inventario_bodega_principal();
        DROP TRIGGER inventario_kardex ON products;
        DROP TRIGGER inventario_historial_inmutable ON "MovimientoInventario";
        DO $$ BEGIN
          IF EXISTS (SELECT 1 FROM products p WHERE p."Quantity" <>
            COALESCE((SELECT sum(e."Cantidad") FROM "ExistenciaBodega" e WHERE e."EmpresaId"=p."EmpresaId" AND e."ProductoId"=p."Id"),0)) THEN
            RAISE EXCEPTION 'Migración cancelada: los saldos por bodega no coinciden con el producto. Revisá antes de continuar.';
          END IF;
          IF EXISTS (SELECT 1 FROM products p JOIN "PrestamoInventario" l ON l."EmpresaId"=p."EmpresaId" AND l."ProductoId"=p."Id"
              WHERE NOT p."IsActive" AND l."Devuelta"<l."Cantidad") THEN
            RAISE EXCEPTION 'Migración cancelada: hay un producto desactivado con préstamos pendientes. Registrá sus devoluciones antes de continuar.';
          END IF;
        END $$;
        """;

    public const string Separar = """
        INSERT INTO "BodegaInventarioHistorica" ("Id","EmpresaId","Nombre") SELECT "Id","EmpresaId","Nombre" FROM "BodegaInventario";
        CREATE TEMP TABLE inventario_productos_antes ON COMMIT DROP AS SELECT * FROM products;
        CREATE TEMP TABLE inventario_categorias_antes ON COMMIT DROP AS SELECT * FROM "CategoriaInventario";
        CREATE TEMP TABLE inventario_mapa_productos ON COMMIT DROP AS
          WITH ubicaciones AS (
            SELECT "EmpresaId","ProductoId" AS anterior,"BodegaId" FROM "ExistenciaBodega"
            UNION SELECT "EmpresaId","ProductoId","BodegaId" FROM "MovimientoInventario"
            UNION SELECT "EmpresaId","ProductoId","BodegaOrigenId" FROM "PrestamoInventario"
            UNION SELECT "EmpresaId","Id",(SELECT min(b."Id") FROM "BodegaInventario" b WHERE b."EmpresaId"=p."EmpresaId") FROM products p
              WHERE NOT EXISTS (SELECT 1 FROM "ExistenciaBodega" e WHERE e."EmpresaId"=p."EmpresaId" AND e."ProductoId"=p."Id")
          ) SELECT "EmpresaId",anterior,"BodegaId",
            CASE WHEN row_number() OVER(PARTITION BY "EmpresaId",anterior ORDER BY "BodegaId")=1 THEN anterior
              ELSE nextval(pg_get_serial_sequence('products','Id'))::integer END AS nuevo FROM ubicaciones;
        DO $$ BEGIN
          IF EXISTS(SELECT 1 FROM inventario_mapa_productos WHERE "BodegaId" IS NULL) THEN RAISE EXCEPTION 'Migración cancelada: hay un producto sin bodega de origen.'; END IF;
        END $$;
        CREATE TEMP TABLE inventario_mapa_categorias ON COMMIT DROP AS
          WITH ubicaciones AS (
            SELECT DISTINCT c."EmpresaId", c."Id" AS anterior,m."BodegaId" FROM inventario_categorias_antes c
              JOIN inventario_productos_antes p ON p."EmpresaId"=c."EmpresaId" AND p."CategoriaId"=c."Id"
              JOIN inventario_mapa_productos m ON m."EmpresaId"=p."EmpresaId" AND m.anterior=p."Id"
            UNION SELECT c."EmpresaId",c."Id",(SELECT min(b."Id") FROM "BodegaInventario" b WHERE b."EmpresaId"=c."EmpresaId") FROM inventario_categorias_antes c
              WHERE NOT EXISTS(SELECT 1 FROM inventario_productos_antes p WHERE p."EmpresaId"=c."EmpresaId" AND p."CategoriaId"=c."Id")
          ) SELECT "EmpresaId",anterior,"BodegaId",
            CASE WHEN row_number() OVER(PARTITION BY "EmpresaId",anterior ORDER BY "BodegaId")=1 THEN anterior
              ELSE nextval(pg_get_serial_sequence('"CategoriaInventario"','Id'))::integer END AS nuevo FROM ubicaciones;
        INSERT INTO "CategoriaInventario" ("Id","EmpresaId","BodegaId","Nombre","NombreNormalizado","Activa")
          SELECT m.nuevo,c."EmpresaId",m."BodegaId",c."Nombre",c."NombreNormalizado",c."Activa"
          FROM inventario_categorias_antes c JOIN inventario_mapa_categorias m ON m."EmpresaId"=c."EmpresaId" AND m.anterior=c."Id" WHERE m.nuevo<>m.anterior;
        UPDATE "CategoriaInventario" c SET "BodegaId"=m."BodegaId" FROM inventario_mapa_categorias m WHERE m.nuevo=m.anterior AND c."EmpresaId"=m."EmpresaId" AND c."Id"=m.anterior;
        INSERT INTO products ("Id","EmpresaId","BodegaId","Code","Name","Quantity","MinimumStock","UnitPrice","UpdatedAtUtc","Unit","AllowsFractions","IsActive","Version","CategoriaId")
          SELECT m.nuevo,p."EmpresaId",m."BodegaId",p."Code",p."Name",COALESCE(e."Cantidad",0),p."MinimumStock",p."UnitPrice",p."UpdatedAtUtc",p."Unit",p."AllowsFractions",p."IsActive",p."Version",c.nuevo
          FROM inventario_productos_antes p JOIN inventario_mapa_productos m ON m."EmpresaId"=p."EmpresaId" AND m.anterior=p."Id"
          LEFT JOIN "ExistenciaBodega" e ON e."EmpresaId"=m."EmpresaId" AND e."ProductoId"=m.anterior AND e."BodegaId"=m."BodegaId"
          LEFT JOIN inventario_mapa_categorias c ON c."EmpresaId"=p."EmpresaId" AND c.anterior=p."CategoriaId" AND c."BodegaId"=m."BodegaId" WHERE m.nuevo<>m.anterior;
        UPDATE products p SET "BodegaId"=m."BodegaId","Quantity"=COALESCE(e."Cantidad",0),"CategoriaId"=c.nuevo
          FROM inventario_mapa_productos m LEFT JOIN "ExistenciaBodega" e ON e."EmpresaId"=m."EmpresaId" AND e."ProductoId"=m.anterior AND e."BodegaId"=m."BodegaId"
          LEFT JOIN inventario_productos_antes a ON a."EmpresaId"=m."EmpresaId" AND a."Id"=m.anterior
          LEFT JOIN inventario_mapa_categorias c ON c."EmpresaId"=m."EmpresaId" AND c.anterior=a."CategoriaId" AND c."BodegaId"=m."BodegaId"
          WHERE m.nuevo=m.anterior AND p."EmpresaId"=m."EmpresaId" AND p."Id"=m.anterior;
        UPDATE "ExistenciaBodega" e SET "ProductoId"=m.nuevo FROM inventario_mapa_productos m WHERE e."EmpresaId"=m."EmpresaId" AND e."ProductoId"=m.anterior AND e."BodegaId"=m."BodegaId";
        UPDATE "MovimientoInventario" e SET "ProductoId"=m.nuevo,"SaldoAnterior"=e."SaldoBodegaAnterior","Cambio"=e."CambioBodega","SaldoPosterior"=e."SaldoBodegaPosterior"
          FROM inventario_mapa_productos m WHERE e."EmpresaId"=m."EmpresaId" AND e."ProductoId"=m.anterior AND e."BodegaId"=m."BodegaId";
        UPDATE "PrestamoInventario" e SET "ProductoId"=m.nuevo FROM inventario_mapa_productos m WHERE e."EmpresaId"=m."EmpresaId" AND e."ProductoId"=m.anterior AND e."BodegaOrigenId"=m."BodegaId";
        INSERT INTO "ProductoInventarioHistorico" ("Id","EmpresaId","BodegaId","OrigenCompartidoId","Code","Name","Unit","AllowsFractions","MinimumStock","UnitPrice","EliminadoEnUtc")
          SELECT p."Id",p."EmpresaId",p."BodegaId",CASE WHEN (SELECT count(*) FROM inventario_mapa_productos x WHERE x."EmpresaId"=m."EmpresaId" AND x.anterior=m.anterior)>1 THEN m.anterior ELSE NULL END,
            p."Code",p."Name",p."Unit",p."AllowsFractions",p."MinimumStock",p."UnitPrice",CASE WHEN NOT p."IsActive" THEN now() END
          FROM products p JOIN inventario_mapa_productos m ON m."EmpresaId"=p."EmpresaId" AND m.nuevo=p."Id";
        INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
          SELECT p."EmpresaId",'Sistema','Editado','Producto',p."Id"::text,left('Separación por bodega · registro compartido #'||h."OrigenCompartidoId"||' · '||p."Name",600),now()
          FROM products p JOIN "ProductoInventarioHistorico" h ON h."EmpresaId"=p."EmpresaId" AND h."Id"=p."Id" WHERE h."OrigenCompartidoId" IS NOT NULL;
        INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
          SELECT "EmpresaId",'Sistema','Eliminado','Producto',"Id"::text,left('Borrado físico de un registro anteriormente desactivado · '||"Name",600),now() FROM products WHERE NOT "IsActive";
        DELETE FROM "ExistenciaBodega" e USING products p WHERE p."EmpresaId"=e."EmpresaId" AND p."Id"=e."ProductoId" AND NOT p."IsActive";
        DELETE FROM products WHERE NOT "IsActive";
        UPDATE products SET "CategoriaId"=NULL WHERE "CategoriaId" IN (SELECT "Id" FROM "CategoriaInventario" WHERE NOT "Activa");
        INSERT INTO "RegistroAuditoria" ("EmpresaId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
          SELECT "EmpresaId",'Sistema','Eliminado','Categoria',"Id"::text,left('Borrado físico de un registro anteriormente desactivado · '||"Nombre",600),now() FROM "CategoriaInventario" WHERE NOT "Activa";
        DELETE FROM "CategoriaInventario" WHERE NOT "Activa";
        UPDATE "BodegaInventarioHistorica" h SET "EliminadaEnUtc"=now() FROM "BodegaInventario" b WHERE h."EmpresaId"=b."EmpresaId" AND h."Id"=b."Id"
          AND (b."Predeterminada" OR NOT b."Activa") AND NOT EXISTS(SELECT 1 FROM products p WHERE p."EmpresaId"=b."EmpresaId" AND p."BodegaId"=b."Id")
          AND NOT EXISTS(SELECT 1 FROM "CategoriaInventario" c WHERE c."EmpresaId"=b."EmpresaId" AND c."BodegaId"=b."Id");
        DELETE FROM "ExistenciaBodega" e USING "BodegaInventarioHistorica" h WHERE h."EmpresaId"=e."EmpresaId" AND h."Id"=e."BodegaId" AND h."EliminadaEnUtc" IS NOT NULL;
        DELETE FROM "BodegaInventario" b USING "BodegaInventarioHistorica" h WHERE h."EmpresaId"=b."EmpresaId" AND h."Id"=b."Id" AND h."EliminadaEnUtc" IS NOT NULL;
        UPDATE "BodegaInventario" SET "Predeterminada"=false,"Activa"=true;
        """;

    public const string Instalar = """
        CREATE FUNCTION inventario_historia_bodega() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
          IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."EmpresaId"<>OLD."EmpresaId") THEN RAISE EXCEPTION 'La identidad de una bodega no se cambia'; END IF;
          IF TG_OP='DELETE' THEN UPDATE "BodegaInventarioHistorica" SET "EliminadaEnUtc"=now() WHERE "EmpresaId"=OLD."EmpresaId" AND "Id"=OLD."Id"; RETURN NULL; END IF;
          IF EXISTS(SELECT 1 FROM "BodegaInventarioHistorica" WHERE "EmpresaId"=NEW."EmpresaId" AND "Id"=NEW."Id" AND "EliminadaEnUtc" IS NOT NULL) THEN RAISE EXCEPTION 'La identidad de una bodega eliminada no se reutiliza'; END IF;
          INSERT INTO "BodegaInventarioHistorica" ("Id","EmpresaId","Nombre") VALUES(NEW."Id",NEW."EmpresaId",NEW."Nombre")
            ON CONFLICT ("EmpresaId","Id") DO UPDATE SET "Nombre"=EXCLUDED."Nombre";
          RETURN NULL;
        END $$;
        CREATE TRIGGER inventario_historia_bodega AFTER INSERT OR UPDATE OR DELETE ON "BodegaInventario" FOR EACH ROW EXECUTE FUNCTION inventario_historia_bodega();
        CREATE FUNCTION inventario_historia_producto() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
          IF TG_OP='DELETE' THEN
            IF EXISTS(SELECT 1 FROM "PrestamoInventario" WHERE "EmpresaId"=OLD."EmpresaId" AND "ProductoId"=OLD."Id" AND "Devuelta"<"Cantidad") THEN RAISE EXCEPTION 'El producto tiene préstamos pendientes'; END IF;
            UPDATE "ProductoInventarioHistorico" SET "EliminadoEnUtc"=now() WHERE "EmpresaId"=OLD."EmpresaId" AND "Id"=OLD."Id"; RETURN NULL;
          END IF;
          IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."EmpresaId"<>OLD."EmpresaId" OR NEW."BodegaId"<>OLD."BodegaId") THEN RAISE EXCEPTION 'La ficha no cambia de identidad, empresa ni bodega'; END IF;
          IF EXISTS(SELECT 1 FROM "ProductoInventarioHistorico" WHERE "EmpresaId"=NEW."EmpresaId" AND "Id"=NEW."Id" AND "EliminadoEnUtc" IS NOT NULL) THEN RAISE EXCEPTION 'La identidad de un producto eliminado no se reutiliza'; END IF;
          INSERT INTO "ProductoInventarioHistorico" ("Id","EmpresaId","BodegaId","Code","Name","Unit","AllowsFractions","MinimumStock","UnitPrice")
            VALUES(NEW."Id",NEW."EmpresaId",NEW."BodegaId",NEW."Code",NEW."Name",NEW."Unit",NEW."AllowsFractions",NEW."MinimumStock",NEW."UnitPrice")
            ON CONFLICT ("EmpresaId","Id") DO UPDATE SET "Code"=EXCLUDED."Code","Name"=EXCLUDED."Name","Unit"=EXCLUDED."Unit","AllowsFractions"=EXCLUDED."AllowsFractions","MinimumStock"=EXCLUDED."MinimumStock","UnitPrice"=EXCLUDED."UnitPrice";
          RETURN NULL;
        END $$;
        CREATE TRIGGER inventario_historia_producto AFTER INSERT OR UPDATE OR DELETE ON products FOR EACH ROW EXECUTE FUNCTION inventario_historia_producto();
        CREATE OR REPLACE FUNCTION inventario_kardex() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE
          contexto jsonb := COALESCE(NULLIF(current_setting('inventario.movimiento', true), ''), '{}')::jsonb;
          previo numeric := 0; clase text; autor integer := NULL; correo text := 'Sistema';
          movimiento_id bigint; precio numeric; cantidad numeric; anterior_bodega numeric;
        BEGIN
          clase := COALESCE(contexto->>'Tipo', CASE WHEN TG_OP='INSERT' THEN 'Inicial' ELSE 'Ajuste' END);
          IF TG_OP='UPDATE' THEN previo:=OLD."Quantity"; IF NEW."Quantity"=previo AND clase<>'Conteo' THEN RETURN NULL; END IF; END IF;
          IF NOT EXISTS(SELECT 1 FROM "BodegaInventario" WHERE "EmpresaId"=NEW."EmpresaId" AND "Id"=NEW."BodegaId" AND "Activa") OR
             (contexto->>'BodegaId' IS NOT NULL AND (contexto->>'BodegaId')::integer<>NEW."BodegaId") THEN RAISE EXCEPTION 'Bodega inválida para este producto'; END IF;
          cantidad:=COALESCE((contexto->>'Cantidad')::numeric,abs(NEW."Quantity"-previo));
          IF clase IN ('TrasladoSalida','TrasladoEntrada') AND (cantidad<=0 OR contexto->>'TrasladoId' IS NULL OR
             NEW."Quantity"-previo<>CASE WHEN clase='TrasladoSalida' THEN -cantidad ELSE cantidad END) THEN RAISE EXCEPTION 'Traslado inválido'; END IF;
          INSERT INTO "ExistenciaBodega" ("EmpresaId","ProductoId","BodegaId","Cantidad") VALUES(NEW."EmpresaId",NEW."Id",NEW."BodegaId",0) ON CONFLICT DO NOTHING;
          SELECT "Cantidad" INTO anterior_bodega FROM "ExistenciaBodega" WHERE "EmpresaId"=NEW."EmpresaId" AND "ProductoId"=NEW."Id" AND "BodegaId"=NEW."BodegaId" FOR UPDATE;
          IF anterior_bodega<>previo THEN RAISE EXCEPTION 'Saldo inconsistente del producto'; END IF;
          UPDATE "ExistenciaBodega" SET "Cantidad"=NEW."Quantity" WHERE "EmpresaId"=NEW."EmpresaId" AND "ProductoId"=NEW."Id" AND "BodegaId"=NEW."BodegaId";
          IF contexto->>'AutorId' IS NOT NULL THEN
            SELECT "Id","Correo" INTO autor,correo FROM "Usuario" WHERE "Id"=(contexto->>'AutorId')::integer AND "EmpresaId"=NEW."EmpresaId";
            IF NOT FOUND THEN RAISE EXCEPTION 'Autor ajeno a la empresa'; END IF;
          END IF;
          precio:=(contexto->>'PrecioUnitario')::numeric;
          INSERT INTO "MovimientoInventario" ("EmpresaId","ProductoId","PrestamoId","Tipo","Unidad","Cantidad","SaldoAnterior","Cambio","SaldoPosterior",
            "BodegaId","SaldoBodegaAnterior","CambioBodega","SaldoBodegaPosterior","TrasladoId","Motivo","Referencia","Cliente","PrecioUnitario","Total","AutorId","CorreoAutor","FechaUtc","SolicitudId","SolicitudDatos")
          VALUES(NEW."EmpresaId",NEW."Id",(contexto->>'PrestamoId')::integer,clase,NEW."Unit",cantidad,previo,NEW."Quantity"-previo,NEW."Quantity",
            NEW."BodegaId",previo,NEW."Quantity"-previo,NEW."Quantity",(contexto->>'TrasladoId')::uuid,COALESCE(contexto->>'Motivo','Cambio desde un proceso directo'),
            contexto->>'Referencia',contexto->>'Cliente',precio,round(precio*cantidad,2),autor,correo,now(),(contexto->>'SolicitudId')::uuid,contexto->>'SolicitudDatos') RETURNING "Id" INTO movimiento_id;
          INSERT INTO "RegistroAuditoria" ("EmpresaId","AutorId","CorreoAutor","Accion","Entidad","EntidadId","Detalle","FechaUtc")
          VALUES(NEW."EmpresaId",autor,correo,'Creado','Movimiento',movimiento_id::text,left(NEW."Name"||' · '||clase||' · Bodega #'||NEW."BodegaId"||': '||previo||' → '||NEW."Quantity",600),now());
          RETURN NULL;
        END $$;
        CREATE TRIGGER inventario_kardex AFTER INSERT OR UPDATE ON products FOR EACH ROW EXECUTE FUNCTION inventario_kardex();
        CREATE TRIGGER inventario_historial_inmutable BEFORE UPDATE OR DELETE ON "MovimientoInventario" FOR EACH ROW EXECUTE FUNCTION inventario_historial_inmutable();
        """;
}
