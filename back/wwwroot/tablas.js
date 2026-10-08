const tablas = (() => {
  const size = 10, states = new Map(), controls = new Map();
  const names = { products: "productos", users: "usuarios", roles: "roles", "category-list": "categorías",
    "groups-list": "grupos", "tasks-list": "tareas", "audit-list": "auditoría", "stock-history": "kárdex",
    "stock-loans": "préstamos", "deleted-list": "productos eliminados" };

  function control(id, body) {
    if (controls.has(id)) return controls.get(id);
    const nav = document.createElement("nav");
    nav.className = "d-flex flex-wrap align-items-center justify-content-center gap-3 p-3";
    nav.dataset.tablePagination = id; nav.setAttribute("aria-label", `Paginación de ${names[id] ?? id}`);
    const status = document.createElement("span"); status.className = "small text-body-secondary";
    status.setAttribute("role", "status"); status.setAttribute("aria-live", "polite"); status.setAttribute("aria-atomic", "true");
    const list = document.createElement("ul"); list.className = "pagination pagination-sm mb-0";
    const button = (label, direction) => {
      const item = document.createElement("li"); item.className = "page-item";
      const node = document.createElement("button"); node.type = "button"; node.className = "page-link";
      node.textContent = label; node.dataset.pageDirection = direction; node.setAttribute("aria-controls", id);
      node.addEventListener("click", () => change(id, direction)); item.append(node); list.append(item); return node;
    };
    const previous = button("Anterior", -1), next = button("Siguiente", 1);
    nav.append(status, list); body.closest(".table-responsive").after(nav);
    const ui = { nav, status, previous, next }; controls.set(id, ui); return ui;
  }

  function display(state) {
    const { rows, page, body, ui, busy, hasMore } = state;
    const start = page * size, visible = rows.slice(start, start + size);
    body.replaceChildren(...(rows.length ? visible : state.empty));
    ui.nav.hidden = rows.length <= size && !hasMore;
    ui.nav.classList.toggle("hidden", ui.nav.hidden);
    ui.status.textContent = rows.length ? `Página ${page + 1} · ${start + 1}–${start + visible.length} de ${rows.length}${hasMore ? " cargadas" : ""}` : "Sin registros";
    ui.previous.disabled = busy || page === 0;
    ui.next.disabled = busy || (start + size >= rows.length && !hasMore);
    [ui.previous, ui.next].forEach(node => node.parentElement.classList.toggle("disabled", node.disabled));
    ui.nav.setAttribute("aria-busy", String(busy));
  }

  async function change(id, direction) {
    const state = states.get(id);
    if (!state || state.busy) return;
    const target = state.page + direction;
    if (target < 0) return;
    if (target * size < state.rows.length) { state.page = target; display(state); return; }
    if (direction !== 1 || !state.hasMore || !state.onMore) return;
    state.busy = true; display(state);
    try { await state.onMore(); }
    finally { if (states.get(id) === state) { state.busy = false; display(state); } }
  }

  function paginate(id, { empty = false, append = false, hasMore = false, onMore = null } = {}) {
    const body = document.getElementById(id), rendered = Array.from(body.children), old = states.get(id);
    const rows = empty ? [] : rendered;
    const page = append && old ? Math.min(old.page + 1, Math.max(0, Math.ceil(rows.length / size) - 1)) : 0;
    const state = { body, rows, empty: empty ? rendered : [], page, hasMore, onMore, busy: false, ui: control(id, body) };
    states.set(id, state); display(state);
  }

  function reset(...ids) {
    for (const id of ids.length ? ids : [...states.keys()]) {
      const state = states.get(id); states.delete(id);
      state?.body.replaceChildren();
      const ui = controls.get(id);
      if (ui) { ui.nav.hidden = true; ui.nav.classList.add("hidden"); ui.previous.disabled = true; ui.next.disabled = true; ui.status.textContent = ""; ui.nav.setAttribute("aria-busy", "false"); }
    }
  }
  return { paginate, reset };
})();
