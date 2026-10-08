const catalogos = (() => {
  const q = selector => document.querySelector(selector);
  let categories = [], warehouses = [], balances = [], selectedWarehouse = null;
  const downloads = new WeakMap();
  function options(id, items, placeholder) {
    const input = q('#' + id), previous = input.value;
    input.replaceChildren(...(placeholder === null ? [] : [new Option(placeholder, '')]),
      ...items.map(i => new Option(i.nombre + (i.activa ? '' : ' (inactiva)'), i.id)));
    if ([...input.options].some(o => o.value === previous)) input.value = previous;
  }
  function set(data) {
    categories = data.categorias; warehouses = data.bodegas; balances = data.existencias;
    q('#warehouse-list').innerHTML = warehouses.map(i => `<div class="list-group-item warehouse-row"><button type="button" class="btn btn-link warehouse-open" data-open-warehouse="${i.id}">${escapeHtml(i.nombre)} <span class="text-secondary small ms-2">Abrir →</span></button>${can('inventario.bodegas') ? `<div class="d-flex gap-2"><button type="button" class="btn btn-outline-secondary btn-sm" data-edit-catalog="${i.id}">Editar</button><button type="button" class="btn btn-outline-danger btn-sm" data-delete-catalog="${i.id}">Eliminar</button></div>` : ''}</div>`).join('');
    q('#warehouse-empty').classList.toggle('hidden', warehouses.length !== 0);
    q('#warehouse-new').classList.toggle('hidden', !can('inventario.bodegas'));
    q('#categories-open').classList.toggle('hidden', !can('inventario.categorias'));
    q('#product-new').classList.toggle('hidden', !can('inventario.crear'));
    q('#inventory-export').classList.toggle('hidden', !can('inventario.exportar'));
    if (selectedWarehouse && !warehouses.some(w => w.id === selectedWarehouse)) selectedWarehouse = null;
    displayWarehouse();
  }
  function displayWarehouse() {
    q('#warehouse-browser').classList.toggle('hidden', selectedWarehouse !== null);
    q('#warehouse-workspace').classList.toggle('hidden', selectedWarehouse === null);
    q('#product-warehouse-filter').value = selectedWarehouse ?? '';
    const item = warehouses.find(w => w.id === selectedWarehouse);
    q('#warehouse-title').textContent = item?.nombre ?? '';
    options('product-warehouse', item ? [item] : [], null);
    options('product-category', categories, 'Elegí una categoría');
    options('product-category-filter', categories, 'Todas las categorías');
    q('#category-list').innerHTML = categories.map(i => `<tr><td>${escapeHtml(i.nombre)}</td><td class="text-nowrap"><button type="button" class="btn btn-outline-primary btn-sm" data-edit-catalog="${i.id}">Editar</button> <button type="button" class="btn btn-outline-danger btn-sm" data-delete-catalog="${i.id}">Eliminar</button></td></tr>`).join('') || '<tr><td colspan="2">Sin categorías.</td></tr>';
    tablas.paginate('category-list', { empty: !categories.length });
    q('#categories-title').textContent = 'Categorías de la empresa';
  }
  function enter(id) {
    if (!warehouses.some(w => w.id === Number(id))) return;
    if (selectedWarehouse !== Number(id)) {
      q('#product-category-filter').value = '';
    }
    selectedWarehouse = Number(id); displayWarehouse(); clearMessage('message'); render();
  }
  q('#warehouse-back').addEventListener('click', () => {
    selectedWarehouse = null; inventario.reset(); displayWarehouse(); clearMessage('message'); render();
  });
  q('#warehouse-new').addEventListener('click', () => {
    if (!can('inventario.bodegas')) return;
    q('#warehouse-form').reset(); q('#warehouse-id').value = '';
    q('#warehouse-form-title').textContent = 'Crear bodega'; clearMessage('warehouse-message');
    dialogos.open('warehouse-dialog', 'warehouse-name');
  });
  q('#categories-open').addEventListener('click', () => {
    if (can('inventario.categorias')) dialogos.open('inventory-catalogs', 'category-name');
  });
  function reset() {
    selectedWarehouse = null;
    categories = []; warehouses = []; balances = [];
    for (const type of ['category', 'warehouse']) { q('#' + type + '-form').reset(); q('#' + type + '-id').value = ''; q('#' + type + '-list').replaceChildren(); }
    q('#product-category-filter').value = ''; q('#product-warehouse-filter').value = '';
    clearMessage('catalog-message'); clearMessage('warehouse-message'); displayWarehouse();
    q('#inventory-export').disabled = false;
  }
  for (const [type, route, permission] of [['category', 'categorias', 'inventario.categorias'], ['warehouse', 'bodegas', 'inventario.bodegas']]) {
    const form = q('#' + type + '-form');
    q('#' + type + '-cancel').addEventListener('click', () => { form.reset(); q('#' + type + '-id').value = ''; });
    form.addEventListener('submit', event => {
      event.preventDefault(); if (!can(permission)) return;
      const id = q('#' + type + '-id').value;
      const message = type === 'warehouse' ? 'warehouse-message' : 'catalog-message';
      submitForm(form, message, async current => {
        const body = { nombre: q('#' + type + '-name').value.trim(), activa: true };
        const saved = await request(`inventario/${route}${id ? '/' + id : ''}`, { method: id ? 'PUT' : 'POST', body });
        if (!current()) return;
        form.reset(); q('#' + type + '-id').value = ''; await loadProducts();
        if (!current()) return;
        if (type === 'warehouse') { await dialogos.close('warehouse-dialog'); if (!id) enter(saved.id); }
        else setMessage(message, 'Categoría guardada.');
      });
    });
    q('#' + type + '-list').addEventListener('click', async event => {
      const button = event.target.closest('button'); if (!button) return;
      if (type === 'warehouse' && button.dataset.openWarehouse) { enter(button.dataset.openWarehouse); return; }
      if (!can(permission)) return;
      const item = (type === 'category' ? categories : warehouses).find(i => String(i.id) === (button.dataset.editCatalog || button.dataset.deleteCatalog));
      if (!item) return;
      if (button.dataset.editCatalog) {
        q('#' + type + '-id').value = item.id; q('#' + type + '-name').value = item.nombre;
        if (type === 'warehouse') { q('#warehouse-form-title').textContent = 'Editar bodega'; clearMessage('warehouse-message'); dialogos.open('warehouse-dialog', 'warehouse-name'); }
        else q('#' + type + '-name').focus(); return;
      }
      if (!confirm(`¿Eliminar ${item.nombre}?`)) return;
      button.disabled = true; const view = viewVersion;
      const message = type === 'warehouse' ? 'message' : 'catalog-message';
      try { await request(`inventario/${route}/${item.id}`, { method: 'DELETE' });
        if (view === viewVersion) { await loadProducts(); if (view === viewVersion) setMessage(message, 'Eliminado.'); }
      } catch (error) { if (view === viewVersion) handleError(message, error); }
      finally { button.disabled = false; }
    });
  }
  async function download(path, name, message, button) {
    if (!can('inventario.exportar')) return;
    const operation = (downloads.get(button) ?? 0) + 1; downloads.set(button, operation);
    const view = viewVersion; button.disabled = true;
    try {
      const response = await fetch(`${apiBaseUrl}/api/inventario/exportar/${path}`, { credentials: 'include' });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new ApiError(problem.title || 'No se pudo exportar.', response.status);
      }
      const blob = await response.blob(); if (view !== viewVersion) return;
      const url = URL.createObjectURL(blob), link = document.createElement('a');
      link.href = url; link.download = name; document.body.appendChild(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) { if (view === viewVersion) handleError(message, error); }
    finally { if (downloads.get(button) === operation) button.disabled = false; }
  }
  q('#inventory-export').addEventListener('click', () => {
    const params = new URLSearchParams({ estado: q('#product-status').value, buscar: q('#product-search').value.trim() });
    if (q('#product-category-filter').value) params.set('categoriaId', q('#product-category-filter').value);
    if (q('#product-warehouse-filter').value) params.set('bodegaId', q('#product-warehouse-filter').value);
    download('inventario?' + params, 'inventario.xlsx', 'message', q('#inventory-export'));
  });
  return { set, reset, download, selected: () => selectedWarehouse, enter,
    category: id => categories.find(c => c.id === id)?.nombre ?? 'Sin categoría',
    quantity: (product, warehouse) => balances.find(e => e.productoId === product && e.bodegaId === Number(warehouse))?.cantidad ?? 0,
    hasWarehouse: (product, warehouse) => balances.some(e => e.productoId === product && e.bodegaId === Number(warehouse)),
    warehouse: id => warehouses.find(w => w.id === Number(id))?.nombre ?? 'Bodega' };
})();
