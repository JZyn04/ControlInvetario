const trabajo = (() => {
  const q = selector => document.querySelector(selector);
  let groups = [], people = [], tasks = [], detail = null, selectedGroup = null, deletingState = null, audit = [];
  let auditFilters = {}, auditVersion = 0, taskDetailVersion = 0;
  let groupMemberIds = new Set(), savingGroupMembers = false;
  let groupMemberSelect = null;
  const esc = value => escapeHtml(String(value ?? ""));
  const date = value => new Intl.DateTimeFormat("es-GT", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
  const btn = (action, id, text, style = "secondary") => `<button type="button" class="btn btn-outline-${style} btn-sm" data-action="${action}" data-id="${id}">${text}</button>`;
  function options(selector, items, label, empty) {
    q(selector).replaceChildren(...(empty === undefined ? [] : [new Option(empty, "")]),
      ...items.map(item => new Option(label(item), String(item.id))));
  }
  function badge(state) {
    const node = document.createElement("span");
    node.className = "badge"; node.textContent = state.nombre;
    const color = /^#[0-9a-f]{6}$/i.test(state.color) ? state.color : "#6c757d";
    if (color.toLowerCase() === "#198754") {
      node.className += " bg-success-subtle text-success-emphasis border border-success-subtle";
      return node;
    }
    const [r, g, b] = [1, 3, 5].map(start => parseInt(color.slice(start, start + 2), 16) / 255)
      .map(channel => channel <= .04045 ? channel / 12.92 : ((channel + .055) / 1.055) ** 2.4);
    node.style.backgroundColor = color;
    node.style.color = .2126 * r + .7152 * g + .0722 * b > .179 ? "#000" : "#fff";
    return node;
  }
  async function action(button, message, run, stillCurrent = () => true) {
    const version = viewVersion;
    const valid = () => version === viewVersion && stillCurrent();
    if (button) button.disabled = true;
    clearMessage(message);
    try { await run(valid); }
    catch (error) { if (valid()) handleError(message, error); }
    finally { if (button) button.disabled = false; }
  }
  function onForm(id, message, run, guard) {
    q(id).addEventListener("submit", event => {
      event.preventDefault(); action(event.currentTarget.querySelector('button[type="submit"]'), message, run, guard?.());
    });
  }
  function resetGroup() { q("#group-form").reset(); q("#group-id").value = ""; q("#group-form-title").textContent = "Crear grupo"; }
  function resetState() { q("#state-form").reset(); q("#state-id").value = ""; q("#state-delete-form").classList.add("hidden"); deletingState = null; }
  function resetGroupDetail() {
    destroyGroupMemberSelect();
    selectedGroup = null;
    groupMemberIds.clear(); savingGroupMembers = false;
    q("#group-detail-title").textContent = ""; q("#group-detail-supervisor").textContent = "";
    q("#group-detail-members").replaceChildren(); q("#states-list").replaceChildren();
    q("#group-members-form").classList.add("hidden");
    q("#group-member-select").replaceChildren(); q("#group-member-select").disabled = true;
    q("#group-members").replaceChildren(); q("#group-member-add").disabled = true;
    q("#group-members-save").disabled = true;
    q("#group-members-editor").disabled = true;
    q("#state-form").classList.add("hidden"); resetState(); clearMessage("group-detail-message");
  }
  function groupDetailGuard() {
    const id = selectedGroup?.id, generation = dialogos.version("group-detail");
    return () => id !== undefined && selectedGroup?.id === id && generation === dialogos.version("group-detail");
  }
  function resetTask() {
    q("#task-form").reset(); q("#task-id").value = ""; q("#task-version").value = "";
    q("#task-form-title").textContent = "Crear tarea"; taskGroupChanged();
  }
  function resetNote() { q("#note-form").reset(); q("#note-id").value = ""; }
  function resetTaskDetail() {
    detail = null; taskDetailVersion++;
    ["task-detail-title", "task-detail-info", "task-detail-description"].forEach(id => q("#" + id).textContent = "");
    q("#notes-list").replaceChildren(); q("#task-detail-state").replaceChildren();
    q("#task-status-form").classList.add("hidden"); q("#note-form").classList.add("hidden");
    resetNote(); clearMessage("task-detail-message");
  }
  function taskDetailGuard() {
    const id = detail?.tarea.id, generation = dialogos.version("task-detail");
    return () => id !== undefined && detail?.tarea.id === id && generation === dialogos.version("task-detail");
  }
  function resetAuditFilters() {
    auditFilters = {}; auditVersion++;
    q("#audit-filters").reset(); q("#audit-record-id").disabled = true;
  }
  function reset() {
    tablas.reset("groups-list", "tasks-list", "audit-list");
    groups = []; people = []; tasks = []; audit = []; detail = null; selectedGroup = null;
    ["groups-list", "group-detail-members", "states-list", "tasks-list", "notes-list", "audit-list"].forEach(id => q("#" + id).replaceChildren());
    ["state-delete-form", "audit-more"].forEach(id => q("#" + id).classList.add("hidden"));
    ["group-message", "task-message", "audit-message"].forEach(clearMessage);
    resetTaskDetail(); dialogos.close("task-detail");
    resetGroupDetail(); dialogos.close("group-detail");
    resetGroup(); resetState(); resetTask(); resetNote(); resetAuditFilters();
  }
  async function load(message = "group-message") {
    const version = viewVersion;
    const withTasks = can("tareas.ver"), managesGroups = can("grupos.gestionar");
    const [newGroups, newPeople, newTasks] = await Promise.all([
      request("grupos"), managesGroups ? request("grupos/personas") : Promise.resolve([]),
      withTasks ? request("tareas") : Promise.resolve([])
    ]);
    if (version !== viewVersion) return;
    groups = newGroups; people = newPeople; tasks = newTasks;
    q("#group-editor").classList.toggle("hidden", !managesGroups);
    options("#group-supervisor", people.filter(person => person.puedeSupervisar), person => person.correo, "Elegí supervisor");
    resetGroup(); renderGroups();
    const manageable = groups.filter(group => group.puedeGestionarTareas);
    q("#task-editor").classList.toggle("hidden", !withTasks || manageable.length === 0);
    options("#task-group", manageable, group => group.nombre, "Elegí grupo");
    resetTask(); renderTasks();
    if (selectedGroup) await showGroup(selectedGroup.id);
    if (detail && !tasks.some(task => task.id === detail.tarea.id)) {
      resetTaskDetail(); await dialogos.close("task-detail");
    }
  }
  function renderGroups() {
    q("#groups-list").innerHTML = groups.map(group => `<tr><td>${esc(group.nombre)}</td><td>${esc(group.supervisor?.correo ?? "Sin supervisor")}</td><td>${group.integrantes.length}</td><td><div class="d-flex gap-2">${btn("group-view", group.id, "Abrir")}${can("grupos.gestionar") ? btn("group-edit", group.id, "Editar", "primary") + btn("group-delete", group.id, "Eliminar", "danger") : ""}</div></td></tr>`).join("") || '<tr><td colspan="4" class="text-secondary">No hay grupos.</td></tr>';
    tablas.paginate("groups-list", { empty: !groups.length });
  }
  const personLabel = person => person.nombre ? `${person.nombre} · ${person.correo}` : person.correo;
  function destroyGroupMemberSelect() {
    const previous = groupMemberSelect; groupMemberSelect = null;
    previous?.destroy();
  }
  function renderGroupMembers() {
    const managesMembers = Boolean(selectedGroup && can("grupos.gestionar"));
    q("#group-members-editor").disabled = !managesMembers || savingGroupMembers;
    q("#group-members-save").disabled = !managesMembers || savingGroupMembers;
    q("#group-member-add").disabled = true;
    if (!managesMembers) { destroyGroupMemberSelect(); q("#group-member-select").disabled = true; return; }
    const known = new Map([...selectedGroup.integrantes, ...(selectedGroup.supervisor ? [selectedGroup.supervisor] : []), ...people]
      .map(person => [person.id, person]));
    const available = [...known.values()].filter(person => !groupMemberIds.has(person.id));
    const supervisorId = selectedGroup.supervisor?.id;
    q("#group-members").replaceChildren(...[...groupMemberIds].map(id => {
      const row = document.createElement("li");
      row.className = "list-group-item d-flex align-items-center justify-content-between gap-3";
      const label = document.createElement("span"); label.className = "text-break"; label.textContent = personLabel(known.get(id));
      const remove = document.createElement(id === supervisorId ? "span" : "button");
      if (id === supervisorId) { remove.className = "badge text-bg-secondary"; remove.textContent = "Supervisor"; }
      else {
        remove.type = "button"; remove.className = "btn btn-outline-danger btn-sm flex-shrink-0"; remove.textContent = "Quitar";
        remove.dataset.removeMember = String(id); remove.disabled = savingGroupMembers;
        remove.setAttribute("aria-label", `Quitar ${personLabel(known.get(id))}`);
      }
      row.append(label, remove); return row;
    }));
    if (!groupMemberSelect) {
      if (typeof TomSelect !== "function") {
        q("#group-members-editor").disabled = true; q("#group-members-save").disabled = true;
        q("#group-detail-members").classList.remove("hidden");
        handleError("group-detail-message", new Error("No se pudo cargar el selector de integrantes. Recargá la página.")); return;
      }
      const groupId = selectedGroup.id;
      q("#group-member-select").disabled = false;
      let control;
      control = new TomSelect(q("#group-member-select"), {
        plugins: ["dropdown_input"],
        valueField: "id", labelField: "label", searchField: "label", maxItems: 1,
        create: false, hideSelected: false, closeAfterSelect: true,
        placeholder: "Seleccioná una persona",
        options: available.map(person => ({ id: String(person.id), label: personLabel(person) })), items: [],
        render: {
          item: (person, escape) => `<div>${escape(person.label)}</div>`,
          no_results: data => `<div class="no-results">${data.input.trim() ? "Sin coincidencias" : "No hay personas disponibles"}</div>`
        },
        onChange(value) {
          if (groupMemberSelect !== this || selectedGroup?.id !== groupId) return;
          q("#group-member-add").disabled = !can("grupos.gestionar") || savingGroupMembers || !value ||
            !known.has(Number(value)) || groupMemberIds.has(Number(value));
        }
      });
      groupMemberSelect = control;
      groupMemberSelect.control_input.setAttribute("maxlength", "254");
      const searchLabel = [...known.values()].some(person => person.nombre) ? "Buscar por correo o nombre…" : "Buscar por correo…";
      groupMemberSelect.control_input.setAttribute("placeholder", searchLabel);
      groupMemberSelect.control_input.setAttribute("aria-label", searchLabel);
    } else {
      const value = groupMemberSelect.getValue(), search = groupMemberSelect.control_input.value;
      groupMemberSelect.clear(true); groupMemberSelect.clearOptions();
      groupMemberSelect.addOptions(available.map(person => ({ id: String(person.id), label: personLabel(person) })));
      if (available.some(person => String(person.id) === value)) groupMemberSelect.setValue(value, true);
      groupMemberSelect.setTextboxValue(search); groupMemberSelect.refreshOptions(false);
    }
    if (savingGroupMembers || !available.length) groupMemberSelect.disable(); else groupMemberSelect.enable();
    q("#group-member-add").disabled = savingGroupMembers || !available.some(person => String(person.id) === groupMemberSelect.getValue());
  }
  async function showGroup(id, open = false) {
    destroyGroupMemberSelect();
    selectedGroup = groups.find(group => group.id === Number(id)) ?? null;
    if (!selectedGroup) { resetGroupDetail(); await dialogos.close("group-detail"); return; }
    if (open) clearMessage("group-detail-message");
    q("#group-detail-title").textContent = selectedGroup.nombre;
    q("#group-detail-supervisor").textContent = `Supervisor: ${selectedGroup.supervisor?.correo ?? "Sin supervisor"}`;
    q("#group-detail-members").replaceChildren(...selectedGroup.integrantes.map(person => {
      const li = document.createElement("li"); li.textContent = personLabel(person); return li;
    }));
    const managesMembers = can("grupos.gestionar");
    q("#group-detail-members").classList.toggle("hidden", managesMembers);
    q("#group-members-form").classList.toggle("hidden", !managesMembers);
    groupMemberIds = new Set(managesMembers ? selectedGroup.integrantes.map(person => person.id) : []);
    if (managesMembers && selectedGroup.supervisor) groupMemberIds.add(selectedGroup.supervisor.id);
    q("#group-member-select").replaceChildren();
    renderGroupMembers();
    q("#state-form").classList.toggle("hidden", !selectedGroup.puedeGestionarEstados);
    resetState();
    q("#states-list").replaceChildren(...selectedGroup.estados.map(state => {
      const row = document.createElement("div"); row.className = "list-group-item d-flex flex-wrap justify-content-between align-items-center gap-2";
      row.append(badge(state));
      const actions = document.createElement("div"); actions.className = "d-flex flex-wrap align-items-center gap-2";
      if (state.esPredeterminado) actions.innerHTML = '<span class="badge text-bg-light border">Predeterminado</span>';
      if (selectedGroup.puedeGestionarEstados) actions.innerHTML +=
        (state.esPredeterminado ? "" : btn("state-default", state.id, "Usar por defecto")) + btn("state-edit", state.id, "Editar", "primary") +
        (selectedGroup.estados.length > 1 ? btn("state-delete", state.id, "Eliminar", "danger") : "");
      row.append(actions); return row;
    }));
    if (open) {
      await dialogos.open("group-detail", "group-detail-title");
    }
  }
  function taskGroupChanged() {
    const group = groups.find(item => item.id === Number(q("#task-group").value));
    options("#task-state", group?.estados ?? [], state => state.nombre);
    const defaultState = group?.estados.find(state => state.esPredeterminado);
    if (defaultState) q("#task-state").value = String(defaultState.id);
    options("#task-assignee", (group?.integrantes ?? []).filter(person => person.puedeVerTareas), person => person.correo, "Sin asignar");
    syncAuto();
  }
  function syncAuto() {
    const assigned = Boolean(q("#task-assignee").value);
    q("#task-auto").disabled = assigned;
    if (assigned) q("#task-auto").checked = false;
  }
  function renderTasks() {
    q("#tasks-list").replaceChildren(...tasks.map(task => {
      const row = document.createElement("tr");
      row.innerHTML = `<td class="text-wrap">${esc(task.titulo)}</td><td>${esc(task.grupo)}</td><td>${esc(task.asignadoACorreo ?? (task.permitirAutoasignacion ? "Disponible para tomar" : "Sin asignar"))}</td><td></td><td><div class="d-flex gap-2">${btn("task-view", task.id, "Abrir")}${task.puedeTomar ? btn("task-claim", task.id, "Tomar", "success") : ""}${task.puedeGestionar ? btn("task-edit", task.id, "Editar", "primary") + btn("task-delete", task.id, "Eliminar", "danger") : ""}</div></td>`;
      row.children[3].append(badge(task.estado)); return row;
    }));
    if (!tasks.length) q("#tasks-list").innerHTML = '<tr><td colspan="5" class="text-secondary">No hay tareas.</td></tr>';
    tablas.paginate("tasks-list", { empty: !tasks.length });
  }
  async function showTask(id, open = true) {
    if (!open && detail?.tarea.id !== id) return;
    const version = viewVersion, revision = ++taskDetailVersion, generation = dialogos.version("task-detail");
    const data = await request(`tareas/${id}`);
    if (version !== viewVersion || revision !== taskDetailVersion || generation !== dialogos.version("task-detail")) return;
    if (!open && detail?.tarea.id !== id) return;
    detail = data;
    if (open) clearMessage("task-detail-message");
    q("#task-detail-title").textContent = data.tarea.titulo;
    q("#task-detail-description").textContent = data.tarea.descripcion;
    q("#task-detail-info").textContent = `${data.tarea.grupo} · ${data.tarea.asignadoACorreo ?? "Sin asignar"} · ${data.tarea.estado.nombre}`;
    q("#task-status-form").classList.toggle("hidden", !data.tarea.puedeCambiarEstado);
    options("#task-detail-state", data.estados, state => state.nombre);
    q("#task-detail-state").value = data.tarea.estado.id;
    resetNote(); q("#note-form").classList.toggle("hidden", !data.tarea.puedeAnotar);
    q("#notes-list").innerHTML = data.notas.map(note => `<article class="border rounded p-3 mb-2"><div class="d-flex flex-wrap justify-content-between gap-2"><h4 class="h6">${esc(note.titulo)}</h4>${note.puedeEditar ? `<div class="d-flex gap-2">${btn("note-edit", note.id, "Editar", "primary")}${btn("note-delete", note.id, "Eliminar", "danger")}</div>` : ""}</div><p class="small text-secondary">${esc(note.correoAutor)} · ${note.tipoAutor === "Asignado" ? "Persona asignada" : "Supervisor"} · ${date(note.creadaEnUtc)}${note.actualizadaEnUtc !== note.creadaEnUtc ? ` · Editada: ${date(note.actualizadaEnUtc)}` : ""}</p><p class="text-break mb-0" style="white-space: pre-wrap">${esc(note.descripcion)}</p></article>`).join("") || '<p class="text-secondary">Sin notas.</p>';
    if (open) await dialogos.open("task-detail", "task-detail-title");
  }
  async function loadAudit(more = false) {
    if (!more) tablas.reset("audit-list");
    const version = viewVersion, revision = ++auditVersion;
    const query = new URLSearchParams(Object.entries(auditFilters).filter(([, value]) => value));
    if (more && audit.length) query.set("antesDe", audit[audit.length - 1].id);
    const data = await request("auditoria" + (query.size ? "?" + query : ""));
    if (version !== viewVersion || revision !== auditVersion) return;
    audit = more ? [...audit, ...data] : data;
    q("#audit-list").innerHTML = audit.map(entry => `<tr><td>${date(entry.fechaUtc)}</td><td>${esc(entry.correoAutor)}</td><td>${esc(entry.accion)}</td><td>${esc(entry.entidad)} #${esc(entry.entidadId)}</td><td class="text-wrap">${esc(entry.detalle)}</td><td>${btn("audit-history", entry.id, "Ver historial")}</td></tr>`).join("") || '<tr><td colspan="6" class="text-secondary">No hay movimientos.</td></tr>';
    q("#audit-more").classList.toggle("hidden", data.length < 100);
    tablas.paginate("audit-list", { empty: !audit.length, append: more, hasMore: data.length === 100,
      onMore: () => action(null, "audit-message", () => loadAudit(true)) });
  }

  ["tasks", "groups"].forEach(name => {
    q(`#${name}-tab`).addEventListener("click", () => {
      if (name === "tasks" ? !can("tareas.ver") : !can("grupos.ver") && !can("tareas.ver")) return;
      showTab(name); action(null, name === "tasks" ? "task-message" : "group-message", () => load());
    });
    q(`#${name}-refresh`).addEventListener("click", event => action(event.currentTarget, name === "tasks" ? "task-message" : "group-message", async valid => {
      await load(); if (valid() && name === "tasks" && detail) await showTask(detail.tarea.id, false);
    }));
  });
  q("#audit-tab").addEventListener("click", () => { if (isAdmin()) { showTab("audit"); action(null, "audit-message", () => loadAudit()); } });
  q("#audit-refresh").addEventListener("click", event => action(event.currentTarget, "audit-message", () => loadAudit()));
  q("#audit-more").addEventListener("click", event => action(event.currentTarget, "audit-message", () => loadAudit(true)));
  q("#audit-entity").addEventListener("change", () => {
    q("#audit-record-id").value = "";
    q("#audit-record-id").disabled = !q("#audit-entity").value;
  });
  onForm("#audit-filters", "audit-message", () => {
    auditFilters = { entidad: q("#audit-entity").value, entidadId: q("#audit-record-id").value.trim(), accion: q("#audit-action").value };
    return loadAudit();
  });
  q("#audit-clear").addEventListener("click", event => action(event.currentTarget, "audit-message", () => { resetAuditFilters(); return loadAudit(); }));
  q("#audit-list").addEventListener("click", event => {
    const button = event.target.closest('button[data-action="audit-history"]'); if (!button) return;
    const entry = audit.find(item => item.id === Number(button.dataset.id)); if (!entry) return;
    q("#audit-entity").value = entry.entidad; q("#audit-record-id").disabled = false;
    q("#audit-record-id").value = entry.entidadId; q("#audit-action").value = "";
    auditFilters = { entidad: entry.entidad, entidadId: entry.entidadId };
    action(button, "audit-message", () => loadAudit());
  });
  q("#group-cancel").addEventListener("click", resetGroup);
  q("#group-detail").addEventListener("hide.bs.modal", resetGroupDetail);
  q("#state-cancel").addEventListener("click", resetState);
  q("#state-delete-cancel").addEventListener("click", resetState);
  q("#task-cancel").addEventListener("click", resetTask);
  q("#note-cancel").addEventListener("click", resetNote);
  q("#task-detail").addEventListener("hide.bs.modal", resetTaskDetail);
  q("#task-group").addEventListener("change", taskGroupChanged);
  q("#task-assignee").addEventListener("change", syncAuto);
  onForm("#group-form", "group-message", async valid => {
    if (!can("grupos.gestionar")) return;
    const id = q("#group-id").value;
    const data = await request(id ? `grupos/${id}` : "grupos", { method: id ? "PUT" : "POST", body: {
      nombre: q("#group-name").value.trim(), supervisorId: Number(q("#group-supervisor").value)
    } });
    if (!valid()) return; await load(); if (valid()) await showGroup(data.id, true);
  });
  q("#group-members-form").addEventListener("submit", event => {
    event.preventDefault();
    if (!selectedGroup || !can("grupos.gestionar") || savingGroupMembers || !groupMemberSelect) return;
    action(null, "group-detail-message", async valid => {
      const groupId = selectedGroup.id;
      savingGroupMembers = true; renderGroupMembers();
      try {
        await request(`grupos/${groupId}/integrantes`, { method: "PUT", body: { integrantesIds: [...groupMemberIds] } });
      } finally {
        if (valid()) { savingGroupMembers = false; renderGroupMembers(); }
      }
      if (valid()) await load();
    }, groupDetailGuard());
  });
  q("#group-members-form").addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target === groupMemberSelect?.control_input) event.preventDefault();
  });
  q("#group-member-add").addEventListener("click", () => {
    if (!selectedGroup || !can("grupos.gestionar") || savingGroupMembers || !groupMemberSelect) return;
    const id = Number(groupMemberSelect.getValue());
    if (groupMemberIds.has(id) || ![...people, ...selectedGroup.integrantes].some(person => person.id === id)) return;
    groupMemberIds.add(id); groupMemberSelect.clear(true); groupMemberSelect.setTextboxValue(""); renderGroupMembers();
  });
  q("#group-members").addEventListener("click", event => {
    const button = event.target.closest("button[data-remove-member]");
    if (!button || !selectedGroup || !can("grupos.gestionar") || savingGroupMembers) return;
    const id = Number(button.dataset.removeMember);
    if (id === selectedGroup.supervisor?.id || !groupMemberIds.has(id)) return;
    groupMemberIds.delete(id); renderGroupMembers();
  });
  q("#groups-list").addEventListener("click", event => {
    const button = event.target.closest("button[data-action]"); if (!button) return;
    const group = groups.find(item => item.id === Number(button.dataset.id)); if (!group) return;
    if (button.dataset.action === "group-view") { action(button, "group-message", () => showGroup(group.id, true)); return; }
    if (button.dataset.action === "group-edit") {
      q("#group-id").value = group.id; q("#group-name").value = group.nombre;
      q("#group-supervisor").value = group.supervisor?.id ?? "";
      q("#group-form-title").textContent = "Editar grupo"; q("#group-editor").scrollIntoView({ block: "center", behavior: "smooth" });
    }
    if (button.dataset.action === "group-delete" && confirm(`¿Eliminar el grupo ${group.nombre}?`)) action(button, "group-message", async valid => {
      await request(`grupos/${group.id}`, { method: "DELETE" }); if (valid()) await load();
    });
  });
  onForm("#state-form", "group-detail-message", async valid => {
    if (!selectedGroup) return;
    const id = q("#state-id").value, groupId = selectedGroup.id;
    await request(`grupos/${groupId}/estados${id ? "/" + id : ""}`, { method: id ? "PUT" : "POST", body: {
      nombre: q("#state-name").value.trim(), color: q("#state-color").value
    } }); if (valid()) await load();
  }, groupDetailGuard);
  q("#states-list").addEventListener("click", event => {
    const button = event.target.closest("button[data-action]"); if (!button || !selectedGroup) return;
    const state = selectedGroup.estados.find(item => item.id === Number(button.dataset.id)); if (!state) return;
    if (!selectedGroup.puedeGestionarEstados) return;
    if (button.dataset.action === "state-default") {
      const groupId = selectedGroup.id;
      action(button, "group-detail-message", async valid => {
        await request(`grupos/${groupId}/estados/${state.id}/predeterminado`, { method: "PUT" });
        if (valid()) await load();
      }, groupDetailGuard()); return;
    }
    resetState();
    if (button.dataset.action === "state-edit") {
      q("#state-id").value = state.id; q("#state-name").value = state.nombre; q("#state-color").value = state.color;
    } else {
      deletingState = state.id; q("#state-delete-form").classList.remove("hidden");
      q("#state-delete-title").textContent = `Eliminar «${state.nombre}»`;
      options("#state-destination", selectedGroup.estados.filter(item => item.id !== state.id), item => item.nombre, "Elegí otro estado");
    }
  });
  onForm("#state-delete-form", "group-detail-message", async valid => {
    if (!selectedGroup || !deletingState || !confirm("¿Eliminar el estado y mover sus tareas al estado elegido?")) return;
    await request(`grupos/${selectedGroup.id}/estados/${deletingState}?destinoId=${Number(q("#state-destination").value)}`, { method: "DELETE" });
    if (valid()) await load();
  }, groupDetailGuard);
  onForm("#task-form", "task-message", async valid => {
    const id = q("#task-id").value;
    const data = await request(id ? `tareas/${id}` : "tareas", { method: id ? "PUT" : "POST", body: {
      grupoId: Number(q("#task-group").value), estadoId: Number(q("#task-state").value),
      titulo: q("#task-title").value.trim(), descripcion: q("#task-description").value.trim(),
      asignadoAId: q("#task-assignee").value ? Number(q("#task-assignee").value) : null,
      permitirAutoasignacion: q("#task-auto").checked, version: Number(q("#task-version").value)
    } }); if (!valid()) return; await load(); if (valid()) await showTask(data.id);
  });
  q("#tasks-list").addEventListener("click", event => {
    const button = event.target.closest("button[data-action]"); if (!button) return;
    const task = tasks.find(item => item.id === Number(button.dataset.id)); if (!task) return;
    if (button.dataset.action === "task-view") { action(button, "task-message", () => showTask(task.id)); return; }
    if (button.dataset.action === "task-edit") {
      q("#task-id").value = task.id; q("#task-version").value = task.version;
      q("#task-group").value = task.grupoId; taskGroupChanged();
      q("#task-state").value = task.estado.id; q("#task-assignee").value = task.asignadoAId ?? ""; syncAuto();
      q("#task-title").value = task.titulo; q("#task-description").value = task.descripcion; q("#task-auto").checked = task.permitirAutoasignacion;
      q("#task-form-title").textContent = "Editar tarea"; q("#task-editor").scrollIntoView({ block: "center", behavior: "smooth" }); return;
    }
    if (button.dataset.action === "task-delete" && !confirm(`¿Eliminar «${task.titulo}» y sus notas?`)) return;
    action(button, "task-message", async valid => {
      if (button.dataset.action === "task-claim") await request(`tareas/${task.id}/tomar`, { method: "POST", body: { version: task.version } });
      if (button.dataset.action === "task-delete") await request(`tareas/${task.id}?version=${task.version}`, { method: "DELETE" });
      if (!valid()) return; await load(); if (valid() && button.dataset.action === "task-claim") await showTask(task.id);
    });
  });
  onForm("#task-status-form", "task-detail-message", async valid => {
    if (!detail) return; const id = detail.tarea.id;
    await request(`tareas/${id}/estado`, { method: "PUT", body: { estadoId: Number(q("#task-detail-state").value), version: detail.tarea.version } });
    if (!valid()) return; await load(); if (valid()) await showTask(id, false);
  }, taskDetailGuard);
  onForm("#note-form", "task-detail-message", async valid => {
    if (!detail) return; const id = detail.tarea.id, note = q("#note-id").value;
    await request(`tareas/${id}/notas${note ? "/" + note : ""}`, { method: note ? "PUT" : "POST", body: {
      titulo: q("#note-title").value.trim(), descripcion: q("#note-description").value.trim()
    } }); if (valid()) await showTask(id, false);
  }, taskDetailGuard);
  q("#notes-list").addEventListener("click", event => {
    const button = event.target.closest("button[data-action]"); if (!button || !detail) return;
    const note = detail.notas.find(item => item.id === Number(button.dataset.id)); if (!note) return;
    if (button.dataset.action === "note-edit") {
      q("#note-id").value = note.id; q("#note-title").value = note.titulo; q("#note-description").value = note.descripcion;
      q("#note-form").scrollIntoView({ block: "center", behavior: "smooth" }); return;
    }
    if (!confirm("¿Eliminar esta nota?")) return; const taskId = detail.tarea.id;
    action(button, "task-detail-message", async valid => { await request(`tareas/${taskId}/notas/${note.id}`, { method: "DELETE" }); if (valid()) await showTask(taskId, false); }, taskDetailGuard());
  });
  return { reset, load };
})();
