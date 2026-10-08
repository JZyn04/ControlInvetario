const inventario = (() => {
  const q = selector => document.querySelector(selector);
  const types = [
    ["Entrada", "Entrada / compra", "inventario.entradas"], ["Venta", "Venta", "inventario.salidas"],
    ["Consumo", "Consumo interno", "inventario.salidas"], ["Prestamo", "Préstamo", "inventario.prestamos"],
    ["DevolucionPrestamo", "Devolución de préstamo", "inventario.prestamos"],
    ["Devolucion", "Otra devolución / entrada", "inventario.entradas"], ["Conteo", "Conteo físico / ajuste", "inventario.conteos"]
  ];
  let product = null, history = [], loans = [], selection = 0, historyVersion = 0, loanVersion = 0, loanPage = 1;
  let pendingRequest = null;
  let balances = [], warehouses = [], pendingTransfer = null, deletedProduct = false;
  let deletedItems = [], deletedWarehouse = null, deletedVersion = 0;
  const money = number => new Intl.NumberFormat("es-GT", { style: "currency", currency: "GTQ" }).format(number);
  const esc = value => escapeHtml(String(value ?? ""));
  const label = type => types.find(t => t[0] === type)?.[1] ?? ({ Inicial: "Saldo inicial", Ajuste: "Ajuste anterior", TrasladoSalida: "Traslado: salida", TrasladoEntrada: "Traslado: entrada" }[type] ?? type);
  const warehouseName = id => warehouses.find(w => w.id === Number(id))?.nombre ?? 'Bodega';
  const availableAt = id => balances.find(b => b.bodegaId === Number(id))?.cantidad ?? 0;
  const valid = (view, detail) => view === viewVersion && detail === selection && product !== null;

  function reset() {
    tablas.reset('stock-history', 'stock-loans', 'deleted-list');
    product = null; selection++; historyVersion++; loanVersion++; deletedVersion++;
    deletedItems = []; deletedWarehouse = null; deletedProduct = false;
    q('#deleted-list').replaceChildren(); q('#deleted-more').disabled = false;
    dialogos.close('deleted-dialog');
    history = []; loans = []; pendingRequest = null;
    balances = []; warehouses = []; pendingTransfer = null;
    q('#stock-export').disabled = false;
    dialogos.close('stock-detail');
    q("#stock-form").reset();
    q('#transfer-form').classList.add('hidden'); q('#transfer-form').reset();
    q("#stock-history").replaceChildren(); q("#stock-loans").replaceChildren();
    clearMessage("stock-message");
  }

  async function open(id, focus = true) {
    tablas.reset('stock-history', 'stock-loans');
    const view = viewVersion, detail = ++selection;
    product = null; pendingRequest = null; history = []; loans = [];
    pendingTransfer = null; balances = []; warehouses = [];
    q('#transfer-form').classList.add('hidden'); q('#transfer-form').reset();
    q("#stock-form").classList.add("hidden");
    q("#stock-history").replaceChildren(); q("#stock-loans").replaceChildren();
    q("#stock-title").textContent = "Cargando producto…"; q("#stock-summary").textContent = "";
    q("#stock-form").reset();
    q("#stock-history-type").value = ""; q("#stock-loans-closed").checked = false;
    clearMessage("stock-message");
    try {
      const [data, people] = await Promise.all([
        request(`inventario/productos/${id}/kardex`),
        can("inventario.prestamos") ? request("inventario/personas") : Promise.resolve([])
      ]);
      if (view !== viewVersion || detail !== selection) return;
      product = data.producto; history = data.movimientos; deletedProduct = data.eliminado === true;
      balances = data.existencias; warehouses = data.bodegas;
      const activeWarehouses = warehouses.filter(w => w.activa);
      const origin = warehouses.find(w => w.id === product.bodegaId);
      for (const id of ['stock-warehouse', 'transfer-origin']) {
        q('#' + id).replaceChildren(...(origin ? [new Option(origin.nombre, origin.id)] : []));
        q('#' + id).disabled = true;
      }
      q('#transfer-destination').replaceChildren(...activeWarehouses.filter(w => w.id !== product.bodegaId).map(w => new Option(w.nombre, w.id)));
      q('#stock-history-warehouse').replaceChildren(new Option('Esta bodega', ''), ...(origin ? [new Option(origin.nombre, origin.id)] : []));
      q('#transfer-form').classList.toggle('hidden', !product.isActive || !can('inventario.traslados') || activeWarehouses.length < 2);
      q('#stock-transfer-tab').classList.toggle('hidden', !product.isActive || !can('inventario.traslados') || activeWarehouses.length < 2);
      q('#stock-loans-tab').classList.toggle('hidden', !can('inventario.prestamos'));
      q('#stock-export').classList.toggle('hidden', !can('inventario.exportar'));
      q('#stock-export-from').value = ''; q('#stock-export-to').value = '';
      q('#transfer-quantity').step = product.allowsFractions ? '0.001' : '1';
      q('#transfer-quantity').min = q('#transfer-quantity').step;
      q("#stock-title").textContent = product.code;
      q("#stock-summary").textContent = deletedProduct ? `Producto eliminado · ${warehouseName(product.bodegaId)}` : `Disponible: ${product.quantity} ${product.unit} · Prestado: ${product.loanedQuantity} ${product.unit} · ${warehouseName(product.bodegaId)}`;
      const available = types.filter(t => can(t[2]));
      q("#stock-type").replaceChildren(...available.map(t => new Option(t[1], t[0])));
      q("#stock-type").value = available[0]?.[0] ?? "";
      q("#stock-form").classList.toggle("hidden", !product.isActive || available.length === 0);
      q('#stock-operation-tab').classList.toggle('hidden', !product.isActive || available.length === 0);
      q("#stock-recipient-user").replaceChildren(new Option("Elegí un usuario", ""), ...people.map(p => new Option(p.correo, p.id)));
      q("#stock-price").value = product.unitPrice;
      renderHistory();
      await loadLoans();
      if (!valid(view, detail)) return;
      configure();
      transferProducts();
      if (focus) {
        bootstrap.Tab.getOrCreateInstance(q(product.isActive && available.length ? '#stock-operation-tab' : '#stock-history-tab')).show();
        await dialogos.open('stock-detail');
      }
    } catch (error) {
      if (view === viewVersion && detail === selection) {
        q("#stock-form").classList.add("hidden");
        q("#stock-history").replaceChildren(); q("#stock-loans").replaceChildren();
        tablas.reset('stock-history', 'stock-loans');
        handleError("stock-message", error);
        if (focus && session) await dialogos.open('stock-detail');
      }
    }
  }

  function configure() {
    if (!product) return;
    const type = q("#stock-type").value;
    const sale = type === "Venta", detailed = sale && q("#stock-sale-detail").checked;
    const loan = type === "Prestamo", external = q("#stock-recipient-type").value === "externo", returning = type === "DevolucionPrestamo";
    q("#stock-sale").classList.toggle("hidden", !sale);
    q("#stock-sale-fields").classList.toggle("hidden", !detailed);
    q("#stock-client").required = detailed; q("#stock-price").required = detailed;
    q("#stock-loan-fields").classList.toggle("hidden", !loan);
    q("#stock-user-field").classList.toggle("hidden", external);
    q("#stock-external-field").classList.toggle("hidden", !external);
    q("#stock-recipient-user").required = loan && !external;
    q("#stock-recipient-external").required = loan && external;
    q("#stock-return-field").classList.toggle("hidden", !returning);
    q("#stock-return-loan").required = returning;
    q("#stock-quantity-label").textContent = type === "Conteo" ? `Cantidad contada disponible (${product.unit}) *` : `Cantidad (${product.unit}) *`;
    q("#stock-quantity").step = product.allowsFractions ? "0.001" : "1";
    q("#stock-quantity").min = type === "Conteo" ? "0" : q("#stock-quantity").step;
    q("#stock-quantity").max = ["Venta", "Consumo", "Prestamo"].includes(type) ? String(availableAt(q('#stock-warehouse').value)) : "999999999999.999";
    if (returning) {
      const selected = loans.find(l => String(l.id) === q("#stock-return-loan").value);
      q("#stock-quantity").max = String(selected?.pendiente ?? 0);
    }
    q("#stock-submit").disabled = returning && !q("#stock-return-loan").value;
    q("#stock-total").textContent = money(Number(q("#stock-price").value) * Number(q("#stock-quantity").value));
  }

  function renderHistory(more = false, hasMore = history.length === 100) {
    q("#stock-history").innerHTML = history.map(m => `<tr><td>${esc(new Intl.DateTimeFormat("es-GT", { dateStyle: "short", timeStyle: "short" }).format(new Date(m.fechaUtc)))}</td>
      <td>${esc(label(m.tipo))}</td><td>${esc(warehouseName(m.bodegaId))}</td><td>${m.saldoBodegaAnterior}</td><td>${m.cambioBodega > 0 ? "+" : ""}${m.cambioBodega}</td><td>${m.saldoBodegaPosterior} ${esc(m.unidad)}</td>
      <td class="text-wrap">${esc(m.motivo)}${m.referencia ? `<br><small>${esc(m.referencia)}</small>` : ""}</td><td>${esc(m.correoAutor)}</td>
      <td>${m.trasladoId ? `Traslado ${esc(m.trasladoId)}` : m.prestamoId ? `Préstamo #${m.prestamoId}` : m.cliente ? `${esc(m.cliente)}<br>${money(m.precioUnitario)} × ${m.cantidad} = ${money(m.total)}` : "—"}</td></tr>`).join("") || '<tr><td colspan="9">No hay movimientos.</td></tr>';
    q("#stock-history-more").classList.toggle("hidden", history.length === 0 || history.length % 100 !== 0);
    tablas.paginate('stock-history', { empty: !history.length, append: more, hasMore, onMore: () => loadHistory(true) });
  }

  async function loadHistory(more = false) {
    if (!product) return;
    if (!more) tablas.reset('stock-history');
    const view = viewVersion, detail = selection, load = ++historyVersion;
    const query = new URLSearchParams();
    if (q("#stock-history-type").value) query.set("tipo", q("#stock-history-type").value);
    if (q('#stock-history-warehouse').value) query.set('bodegaId', q('#stock-history-warehouse').value);
    if (more && history.length) query.set("antesDe", history.at(-1).id);
    q("#stock-history-more").disabled = true;
    try {
      const data = await request(`inventario/productos/${product.id}/kardex?${query}`);
      if (!valid(view, detail) || load !== historyVersion) return;
      history = more ? [...history, ...data.movimientos] : data.movimientos;
      renderHistory(more, data.movimientos.length === 100);
      q("#stock-history-more").classList.toggle("hidden", data.movimientos.length < 100);
    } catch (error) { if (valid(view, detail) && load === historyVersion) handleError("stock-message", error); }
    finally { if (valid(view, detail) && load === historyVersion) q("#stock-history-more").disabled = false; }
  }

  async function loadLoans(more = false) {
    if (!product) return;
    if (!more) tablas.reset('stock-loans');
    const view = viewVersion, detail = selection, load = ++loanVersion, page = more ? loanPage + 1 : 1;
    q("#stock-loans-more").disabled = true;
    try {
      const data = await request(`inventario/prestamos?productoId=${product.id}&pendientes=${!q("#stock-loans-closed").checked}&pagina=${page}`);
      if (!valid(view, detail) || load !== loanVersion) return;
      loans = more ? [...loans, ...data] : data; loanPage = page;
      q("#stock-loans").innerHTML = loans.map(l => `<tr><td>${l.id}</td><td>${esc(l.destinatario)} <span class="badge text-bg-secondary">${l.esExterno ? "Externo" : "Empresa"}</span></td>
        <td>${esc(warehouseName(l.bodegaOrigenId))}</td><td>${l.cantidad}</td><td>${l.devuelta}</td><td>${l.pendiente}</td><td>${esc(l.fechaPrevista ?? "—")}</td><td>${l.pendiente > 0 && can("inventario.prestamos") && product.isActive ? `<button type="button" class="btn btn-outline-primary btn-sm" data-return-loan="${l.id}">Devolver</button>` : ""}</td></tr>`).join("") || '<tr><td colspan="8">No hay préstamos.</td></tr>';
      q("#stock-return-loan").replaceChildren(new Option("Elegí un préstamo", ""), ...loans.filter(l => l.pendiente > 0).map(l => new Option(`#${l.id} · ${l.destinatario} · Pendiente: ${l.pendiente}`, l.id)));
      q("#stock-loans-more").classList.toggle("hidden", data.length < 100);
      tablas.paginate('stock-loans', { empty: !loans.length, append: more, hasMore: data.length === 100, onMore: () => loadLoans(true) });
      configure();
    } catch (error) { if (valid(view, detail) && load === loanVersion) handleError("stock-message", error); }
    finally { if (valid(view, detail) && load === loanVersion) q("#stock-loans-more").disabled = false; }
  }

  q("#stock-form").addEventListener("submit", async event => {
    event.preventDefault();
    if (!product || !product.isActive || q("#stock-submit").disabled) return;
    const type = q("#stock-type").value;
    if (!types.some(t => t[0] === type && can(t[2]))) return;
    const body = { productoId: product.id, version: product.version, tipo: type, cantidad: Number(q("#stock-quantity").value),
      bodegaId: product.bodegaId,
      motivo: q("#stock-reason").value.trim(), referencia: q("#stock-reference").value.trim() || null };
    if (type === "Venta" && q("#stock-sale-detail").checked) Object.assign(body, {
      ventaDetallada: true, cliente: q("#stock-client").value.trim(), precioUnitario: Number(q("#stock-price").value)
    });
    if (type === "Prestamo") Object.assign(body, { fechaPrevista: q("#stock-due").value || null,
      ...(q("#stock-recipient-type").value === "externo" ? { destinatarioExterno: q("#stock-recipient-external").value.trim() } : { destinatarioUsuarioId: Number(q("#stock-recipient-user").value) }) });
    if (type === "DevolucionPrestamo") body.prestamoId = Number(q("#stock-return-loan").value);
    const signature = JSON.stringify(body);
    if (!pendingRequest || pendingRequest.signature !== signature) pendingRequest = { signature, id: crypto.randomUUID() };
    body.solicitudId = pendingRequest.id;
    const registered = availableAt(body.bodegaId);
    if (type === "Conteo" && !confirm(`${warehouseName(body.bodegaId)}. Disponible registrado: ${registered} ${product.unit}. Contado: ${body.cantidad}. Diferencia: ${Number((body.cantidad - registered).toFixed(3))}. ¿Confirmar conteo?`)) return;
    const view = viewVersion, detail = selection;
    q("#stock-submit").disabled = true; clearMessage("stock-message");
    try {
      await request("inventario/movimientos", { method: "POST", body });
      if (!valid(view, detail)) return;
      pendingRequest = null;
      await loadProducts();
      if (view === viewVersion) setMessage("stock-message", "Movimiento registrado.");
    } catch (error) {
      if (!valid(view, detail)) return;
      if (error.status === 409) { const id = product.id; await open(id, false); }
      if (view === viewVersion) handleError("stock-message", error);
    } finally { if (view === viewVersion) configure(); }
  });

  for (const id of ["stock-type", "stock-sale-detail", "stock-recipient-type", "stock-return-loan", "stock-warehouse"]) q("#" + id).addEventListener("change", configure);
  for (const id of ["stock-price", "stock-quantity"]) q("#" + id).addEventListener("input", configure);
  q("#stock-close").addEventListener("click", reset);
  q('#stock-detail').addEventListener('hidden.bs.modal', () => {
    tablas.reset('stock-history', 'stock-loans');
    product = null; selection++; historyVersion++; loanVersion++;
    pendingRequest = null; pendingTransfer = null;
  });
  q("#stock-history-type").addEventListener("change", () => loadHistory());
  q('#stock-history-warehouse').addEventListener('change', () => loadHistory());
  q("#stock-history-more").addEventListener("click", () => loadHistory(true));
  q("#stock-loans-closed").addEventListener("change", () => loadLoans());
  q("#stock-loans-more").addEventListener("click", () => loadLoans(true));
  q("#stock-loans").addEventListener("click", event => {
    const button = event.target.closest("button[data-return-loan]");
    if (!button || !product?.isActive || !can("inventario.prestamos")) return;
    const loan = loans.find(l => l.id === Number(button.dataset.returnLoan));
    if (!loan) return;
    q("#stock-type").value = "DevolucionPrestamo"; q("#stock-return-loan").value = String(loan.id);
    q('#stock-warehouse').value = String(loan.bodegaOrigenId);
    q("#stock-quantity").value = loan.pendiente; configure();
    bootstrap.Tab.getOrCreateInstance(q('#stock-operation-tab')).show(); q("#stock-reason").focus();
  });
  function configureTransfer() {
    if (!product) return;
    q('#transfer-quantity').max = String(availableAt(q('#transfer-origin').value));
    q('#transfer-submit').disabled = !product.isActive || !transferTarget();
  }
  function transferTarget() {
    return products.find(p => p.id === Number(q('#transfer-product').value) && p.id !== product.id &&
      p.bodegaId === Number(q('#transfer-destination').value) && p.bodegaId !== product.bodegaId && p.isActive &&
      p.unit.toLocaleLowerCase() === product.unit.toLocaleLowerCase() && p.allowsFractions === product.allowsFractions);
  }
  function transferProducts() {
    if (!product) return;
    const input = q('#transfer-product'), previous = input.value;
    const compatible = products.filter(p => p.bodegaId === Number(q('#transfer-destination').value) && p.bodegaId !== product.bodegaId && p.isActive &&
      p.unit.toLocaleLowerCase() === product.unit.toLocaleLowerCase() && p.allowsFractions === product.allowsFractions);
    input.replaceChildren(new Option('Elegí el producto', ''), ...compatible.map(p => new Option(`${p.code} · ${p.name} · ID ${p.id}`, p.id)));
    if (compatible.some(p => String(p.id) === previous)) input.value = previous;
    q('#transfer-empty').classList.toggle('hidden', compatible.length !== 0);
    configureTransfer();
  }
  q('#transfer-origin').addEventListener('change', configureTransfer);
  q('#transfer-destination').addEventListener('change', transferProducts);
  q('#transfer-product').addEventListener('change', configureTransfer);
  q('#transfer-form').addEventListener('submit', async event => {
    event.preventDefault(); if (!product || !can('inventario.traslados') || q('#transfer-submit').disabled) return;
    const target = transferTarget(); if (!target) return;
    const body = { productoId: product.id, version: product.version, productoDestinoId: target.id, versionDestino: target.version, origenId: product.bodegaId, destinoId: target.bodegaId,
      cantidad: Number(q('#transfer-quantity').value), motivo: q('#transfer-reason').value.trim(), referencia: q('#transfer-reference').value.trim() || null };
    const signature = JSON.stringify(body);
    if (!pendingTransfer || pendingTransfer.signature !== signature) pendingTransfer = { signature, id: crypto.randomUUID() };
    body.solicitudId = pendingTransfer.id;
    const view = viewVersion, detail = selection; q('#transfer-submit').disabled = true; clearMessage('stock-message');
    try { await request('inventario/traslados', { method: 'POST', body });
      if (!valid(view, detail)) return;
      pendingTransfer = null; await loadProducts(); if (view === viewVersion) setMessage('stock-message', 'Traslado registrado.');
    } catch (error) { if (!valid(view, detail)) return;
      if (error.status === 409) await open(product.id, false);
      if (view === viewVersion) handleError('stock-message', error);
    } finally { if (view === viewVersion) configureTransfer(); }
  });
  q('#stock-export').addEventListener('click', () => {
    if (!product) return;
    const params = new URLSearchParams({ productoId: product.id });
    for (const [input, key] of [['stock-history-type', 'tipo'], ['stock-history-warehouse', 'bodegaId'], ['stock-export-from', 'desde'], ['stock-export-to', 'hasta']])
      if (q('#' + input).value) params.set(key, q('#' + input).value);
    catalogos.download('kardex?' + params, 'kardex.xlsx', 'stock-message', q('#stock-export'));
  });
  async function loadDeleted(more = false) {
    if (!more) tablas.reset('deleted-list');
    const view = viewVersion, load = ++deletedVersion, params = new URLSearchParams();
    if (deletedWarehouse) params.set('bodegaId', deletedWarehouse);
    if (more && deletedItems.length) params.set('antesDe', deletedItems.at(-1).id);
    q('#deleted-more').disabled = true;
    try {
      const data = await request('inventario/productos/eliminados?' + params);
      if (view !== viewVersion || load !== deletedVersion) return;
      deletedItems = more ? [...deletedItems, ...data] : data;
      q('#deleted-list').innerHTML = deletedItems.map(p => `<tr><td>${p.id}</td><td>${esc(p.code)}<br>${esc(p.name)}</td><td>${esc(p.bodega)}</td><td>${esc(new Intl.DateTimeFormat('es-GT', { dateStyle: 'short' }).format(new Date(p.eliminadoEnUtc)))}</td><td><button type="button" class="btn btn-outline-secondary btn-sm" data-deleted-product="${p.id}">Ver kárdex</button></td></tr>`).join('') || '<tr><td colspan="5">No hay productos eliminados.</td></tr>';
      q('#deleted-more').classList.toggle('hidden', data.length < 100);
      tablas.paginate('deleted-list', { empty: !deletedItems.length, append: more, hasMore: data.length === 100, onMore: () => loadDeleted(true) });
    } catch (error) { if (view === viewVersion && load === deletedVersion) handleError('deleted-message', error); }
    finally { if (view === viewVersion && load === deletedVersion) q('#deleted-more').disabled = false; }
  }
  for (const id of ['deleted-open', 'deleted-open-all']) q('#' + id).addEventListener('click', async () => {
    if (!can('inventario.ver')) return;
    const view = viewVersion;
    deletedWarehouse = id === 'deleted-open' ? catalogos.selected() : null; deletedItems = [];
    tablas.reset('deleted-list');
    q('#deleted-list').replaceChildren(); clearMessage('deleted-message');
    q('#deleted-title').textContent = deletedWarehouse ? `Productos eliminados · ${catalogos.warehouse(deletedWarehouse)}` : 'Productos eliminados · Todas las bodegas';
    await dialogos.open('deleted-dialog'); if (view === viewVersion) await loadDeleted();
  });
  q('#deleted-dialog').addEventListener('hidden.bs.modal', () => { deletedVersion++; tablas.reset('deleted-list'); });
  q('#deleted-more').addEventListener('click', () => loadDeleted(true));
  q('#deleted-list').addEventListener('click', event => {
    const button = event.target.closest('button[data-deleted-product]');
    if (button && deletedItems.some(p => p.id === Number(button.dataset.deletedProduct))) open(Number(button.dataset.deletedProduct));
  });
  return { reset, open, changed: async items => {
    if (!product) return;
    if (deletedProduct) return;
    const next = items.find(p => p.id === product.id);
    if (!next) reset(); else await open(next.id, false);
  } };
})();
