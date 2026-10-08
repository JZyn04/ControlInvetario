using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class InventarioPorBodegaBorradoReal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(back.Data.InventarioPorBodegaSql.Preparar);
            migrationBuilder.DropForeignKey(
                name: "FK_ExistenciaBodega_products_EmpresaId_ProductoId",
                table: "ExistenciaBodega");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_PrestamoInventario_EmpresaId_ProductoI~",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_products_EmpresaId_ProductoId",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PrestamoInventario_BodegaInventario_EmpresaId_BodegaOrigenId",
                table: "PrestamoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PrestamoInventario_products_EmpresaId_ProductoId",
                table: "PrestamoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_CategoriaId",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_products_EmpresaId_Id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_CategoriaId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_Code",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PrestamoInventario_EmpresaId_ProductoId_Id",
                table: "PrestamoInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoInventario_EmpresaId_ProductoId_PrestamoId",
                table: "MovimientoInventario");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_Id",
                table: "CategoriaInventario");

            migrationBuilder.DropIndex(
                name: "IX_CategoriaInventario_EmpresaId_NombreNormalizado",
                table: "CategoriaInventario");

            migrationBuilder.AddColumn<int>(
                name: "BodegaId",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BodegaId",
                table: "CategoriaInventario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_products_EmpresaId_Id_BodegaId",
                table: "products",
                columns: new[] { "EmpresaId", "Id", "BodegaId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PrestamoInventario_EmpresaId_Id",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_BodegaId_Id",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "BodegaId", "Id" });

            migrationBuilder.CreateTable(
                name: "BodegaInventarioHistorica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EliminadaEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodegaInventarioHistorica", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_BodegaInventarioHistorica_Empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoInventarioHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    BodegaId = table.Column<int>(type: "integer", nullable: false),
                    OrigenCompartidoId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AllowsFractions = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EliminadoEnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoInventarioHistorico", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductoInventarioHistorico_BodegaInventarioHistorica_Empre~",
                        columns: x => new { x.EmpresaId, x.BodegaId },
                        principalTable: "BodegaInventarioHistorica",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(back.Data.InventarioPorBodegaSql.Separar);
            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_BodegaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "BodegaId", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_BodegaId_Code",
                table: "products",
                columns: new[] { "EmpresaId", "BodegaId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_PrestamoId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "PrestamoId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaInventario_EmpresaId_BodegaId_NombreNormalizado",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "BodegaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoInventarioHistorico_EmpresaId_BodegaId",
                table: "ProductoInventarioHistorico",
                columns: new[] { "EmpresaId", "BodegaId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CategoriaInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "BodegaId" },
                principalTable: "BodegaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExistenciaBodega_products_EmpresaId_ProductoId_BodegaId",
                table: "ExistenciaBodega",
                columns: new[] { "EmpresaId", "ProductoId", "BodegaId" },
                principalTable: "products",
                principalColumns: new[] { "EmpresaId", "Id", "BodegaId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_BodegaInventarioHistorica_EmpresaId_Bo~",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "BodegaId" },
                principalTable: "BodegaInventarioHistorica",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_PrestamoInventario_EmpresaId_PrestamoId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "PrestamoId" },
                principalTable: "PrestamoInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_ProductoInventarioHistorico_EmpresaId_~",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId" },
                principalTable: "ProductoInventarioHistorico",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrestamoInventario_BodegaInventarioHistorica_EmpresaId_Bode~",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "BodegaOrigenId" },
                principalTable: "BodegaInventarioHistorica",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrestamoInventario_ProductoInventarioHistorico_EmpresaId_Pr~",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "ProductoId" },
                principalTable: "ProductoInventarioHistorico",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_BodegaInventario_EmpresaId_BodegaId",
                table: "products",
                columns: new[] { "EmpresaId", "BodegaId" },
                principalTable: "BodegaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_BodegaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "BodegaId", "CategoriaId" },
                principalTable: "CategoriaInventario",
                principalColumns: new[] { "EmpresaId", "BodegaId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(back.Data.InventarioPorBodegaSql.Instalar);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El antiguo modelo mezcla identidades que ahora tienen historiales separados.
            // Recuperación: restaurar la copia previa con su binario anterior; nunca borrar estos historiales.
            migrationBuilder.Sql("DO $$ BEGIN RAISE EXCEPTION 'Rollback cancelado: la independencia por bodega requiere restaurar el respaldo previo junto al backend anterior.'; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_CategoriaInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "CategoriaInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_ExistenciaBodega_products_EmpresaId_ProductoId_BodegaId",
                table: "ExistenciaBodega");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_BodegaInventarioHistorica_EmpresaId_Bo~",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_PrestamoInventario_EmpresaId_PrestamoId",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_ProductoInventarioHistorico_EmpresaId_~",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PrestamoInventario_BodegaInventarioHistorica_EmpresaId_Bode~",
                table: "PrestamoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PrestamoInventario_ProductoInventarioHistorico_EmpresaId_Pr~",
                table: "PrestamoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_products_BodegaInventario_EmpresaId_BodegaId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_BodegaId_CategoriaId",
                table: "products");

            migrationBuilder.DropTable(
                name: "ProductoInventarioHistorico");

            migrationBuilder.DropTable(
                name: "BodegaInventarioHistorica");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_products_EmpresaId_Id_BodegaId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_BodegaId_CategoriaId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_EmpresaId_BodegaId_Code",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PrestamoInventario_EmpresaId_Id",
                table: "PrestamoInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoInventario_EmpresaId_PrestamoId",
                table: "MovimientoInventario");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_BodegaId_Id",
                table: "CategoriaInventario");

            migrationBuilder.DropIndex(
                name: "IX_CategoriaInventario_EmpresaId_BodegaId_NombreNormalizado",
                table: "CategoriaInventario");

            migrationBuilder.DropColumn(
                name: "BodegaId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BodegaId",
                table: "CategoriaInventario");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_products_EmpresaId_Id",
                table: "products",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PrestamoInventario_EmpresaId_ProductoId_Id",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CategoriaInventario_EmpresaId_Id",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_products_EmpresaId_Code",
                table: "products",
                columns: new[] { "EmpresaId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_EmpresaId_ProductoId_PrestamoId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "PrestamoId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaInventario_EmpresaId_NombreNormalizado",
                table: "CategoriaInventario",
                columns: new[] { "EmpresaId", "NombreNormalizado" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExistenciaBodega_products_EmpresaId_ProductoId",
                table: "ExistenciaBodega",
                columns: new[] { "EmpresaId", "ProductoId" },
                principalTable: "products",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_BodegaInventario_EmpresaId_BodegaId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "BodegaId" },
                principalTable: "BodegaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_PrestamoInventario_EmpresaId_ProductoI~",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId", "PrestamoId" },
                principalTable: "PrestamoInventario",
                principalColumns: new[] { "EmpresaId", "ProductoId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoInventario_products_EmpresaId_ProductoId",
                table: "MovimientoInventario",
                columns: new[] { "EmpresaId", "ProductoId" },
                principalTable: "products",
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
                name: "FK_PrestamoInventario_products_EmpresaId_ProductoId",
                table: "PrestamoInventario",
                columns: new[] { "EmpresaId", "ProductoId" },
                principalTable: "products",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_CategoriaInventario_EmpresaId_CategoriaId",
                table: "products",
                columns: new[] { "EmpresaId", "CategoriaId" },
                principalTable: "CategoriaInventario",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
