const localApiByPort = { "5070": "http://localhost:5279", "7219": "https://localhost:7201" };
const apiBaseUrl = localApiByPort[window.location.port] ?? window.location.origin;
const $ = selector => document.querySelector(selector);
const form = $("#product-form");
const tbody = $("#products");
const fields = {
  id: $("#product-id"), code: $("#code"), name: $("#name"),
  minimumStock: $("#minimum-stock"), unitPrice: $("#unit-price"), unit: $("#product-unit"),
  version: $("#product-version")
};
let session = null;
let csrfToken = null;
let products = [];
let users = [];
let roles = [];
let permissionCatalog = [];
let userProfile = null, userProfileVersion = 0, editingUserProfile = false;
const selectedRolePermissions = new Set();
let viewVersion = 0;

const can = permission => session?.empresaActiva?.rol.permisos.includes(permission) === true;
const isAdmin = () => session?.empresaActiva?.rol.esAdministrador === true;

class ApiError extends Error {
  constructor(message, status) { super(message); this.status = status; }
}

async function request(path, { method = "GET", body } = {}) {
  const headers = {};
  if (method !== "GET") {
    if (!csrfToken) await refreshCsrf();
    headers["X-CSRF-TOKEN"] = csrfToken;
  }
  if (body !== undefined) headers["Content-Type"] = "application/json";
  let response;
  try {
    response = await fetch(`${apiBaseUrl}/api/${path}`, {
      method, headers, credentials: "include", signal: AbortSignal.timeout(15000),
      ...(body === undefined ? {} : { body: JSON.stringify(body) })
    });
  } catch {
    throw new ApiError("No se pudo conectar con el servidor. Intentá de nuevo en unos segundos.", 0);
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(" ") : null;
    throw new ApiError(validation || problem?.title || (response.status === 403 ? "Tu rol no tiene permiso para esta operación." : "No se pudo completar la operación."), response.status);
  }
  return response.status === 204 ? null : response.json();
}

async function refreshCsrf() {
  csrfToken = (await request("auth/csrf")).token;
}

async function initialize() {
  for (let attempt = 0; attempt < 15; attempt++) {
    try {
      await refreshCsrf();
      const current = await request("auth/me").catch(error => {
        if (error.status === 401) return null;
        throw error;
      });
      $("#connecting").classList.add("hidden");
      if (current) await showSession(current);
      else showAuth();
      return;
    } catch (error) {
      if (error.status === 0 && attempt < 14) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        continue;
      }
      $("#connecting").classList.add("hidden");
      showAuth();
      setMessage("auth-message", error.message, true);
      return;
    }
  }
}

function showAuth() {
  tablas.reset();
  dialogos.reset();
  resetUserProfile();
  session = null;
  csrfToken = null;
  viewVersion++;
  trabajo.reset();
  inventario.reset(); catalogos.reset();
  products = [];
  users = [];
  roles = [];
  tbody.replaceChildren();
  $("#users").replaceChildren();
  resetForm();
  $("#login-form").reset();
  $("#register-form").reset();
  $("#profile-form").reset();
  resetProfilePassword();
  resetUserForm();
  resetRoleForm();
  $("#session-bar").classList.add("hidden");
  $("#dashboard").classList.add("hidden");
  $("#company-panel").classList.add("hidden");
  $("#auth-panel").classList.remove("hidden");
  toggleAuth(false);
}

function toggleAuth(register) {
  $("#login-panel").classList.toggle("hidden", register);
  $("#register-panel").classList.toggle("hidden", !register);
  $("#login-password").value = "";
  $("#register-password").value = "";
  clearMessage("auth-message");
}

async function showSession(current) {
  tablas.reset();
  dialogos.reset();
  resetUserProfile();
  session = current;
  viewVersion++;
  trabajo.reset();
  inventario.reset(); catalogos.reset();
  products = [];
  users = [];
  roles = [];
  tbody.replaceChildren();
  $("#users").replaceChildren();
  $("#auth-panel").classList.add("hidden");
  $("#session-bar").classList.remove("hidden");
  $("#session-email").textContent = current.usuario.correo;
  $("#change-company").classList.toggle("hidden", current.empresas.length < 2);
  $("#login-form").reset();
  $("#register-form").reset();
  resetUserForm();
  resetRoleForm();
  resetForm();
  clearMessage("message");
  clearMessage("user-message");
  clearMessage("role-message");
  clearMessage("company-message");
  if (!current.empresaActiva) {
    showCompanyChooser();
    return;
  }
  $("#company-panel").classList.add("hidden");
  $("#dashboard").classList.remove("hidden");
  $("#empresa-nombre").textContent = current.empresaActiva.nombre;
  $("#session-role").textContent = current.empresaActiva.rol.nombre;
  $("#inventory-tab").classList.toggle("hidden", !can("inventario.ver"));
  $("#tasks-tab").classList.toggle("hidden", !can("tareas.ver"));
  $("#groups-tab").classList.toggle("hidden", !can("grupos.ver") && !can("tareas.ver"));
  $("#audit-tab").classList.toggle("hidden", !isAdmin());
  $("#users-tab").classList.toggle("hidden", !isAdmin());
  $("#roles-tab").classList.toggle("hidden", !isAdmin());
  showTab(can("inventario.ver") ? "inventory" : can("tareas.ver") ? "tasks" : "profile");
  fillProfile(current.usuario);
  resetForm();
  if (isAdmin()) await loadRoles();
  if (can("inventario.ver")) await loadProducts();
  else if (can("tareas.ver")) {
    try { await trabajo.load(); } catch (error) { handleError("task-message", error); }
  }
}

function showCompanyChooser() {
  dialogos.reset();
  resetUserProfile();
  viewVersion++;
  trabajo.reset();
  inventario.reset(); catalogos.reset();
  $("#dashboard").classList.add("hidden");
  $("#company-panel").classList.remove("hidden");
  $("#company-list").replaceChildren(...session.empresas.map(empresa => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center";
    button.dataset.company = empresa.id;
    button.append(document.createTextNode(empresa.nombre));
    const role = document.createElement("span");
    role.className = "badge text-bg-secondary";
    role.textContent = empresa.rol.nombre;
    button.append(role);
    return button;
  }));
}

function showTab(selected) {
  if (selected !== 'inventory') { dialogos.reset(); inventario.reset(); }
  ["inventory", "users", "roles", "profile", "tasks", "groups", "audit"].forEach(name => {
    $("#" + name + "-panel").classList.toggle("hidden", selected !== name);
    const tab = $("#" + name + "-tab");
    const active = name === selected;
    tab.classList.toggle("active", active);
    tab.setAttribute("aria-selected", String(active));
  });
}

const formOperations = new WeakMap();
async function submitForm(target, messageId, action, guard = () => true) {
  const view = viewVersion, modalId = target.closest?.('.modal')?.id;
  const modalVersion = modalId ? dialogos.version(modalId) : null;
  const operation = (formOperations.get(target) ?? 0) + 1; formOperations.set(target, operation);
  const current = () => view === viewVersion && formOperations.get(target) === operation && guard() &&
    (!modalId || dialogos.version(modalId) === modalVersion);
  const button = target.querySelector('button[type="submit"]');
  button.disabled = true;
  clearMessage(messageId);
  try { await action(current); }
  catch (error) { if (current()) handleError(messageId, error); }
  finally { if (formOperations.get(target) === operation) button.disabled = false; }
}

function handleError(messageId, error) {
  if (error.status === 401 && session) {
    showAuth();
    setMessage("auth-message", "Tu sesión terminó. Iniciá sesión nuevamente.", true);
    return;
  }
  setMessage(messageId, error.message, true);
}

$("#login-form").addEventListener("submit", event => {
  event.preventDefault();
  submitForm(event.currentTarget, "auth-message", async () => {
    const current = await request("auth/login", { method: "POST", body: {
      correo: $("#login-email").value.trim(), contrasenia: $("#login-password").value
    } });
    await refreshCsrf();
    await showSession(current);
  });
});

$("#register-form").addEventListener("submit", event => {
  event.preventDefault();
  submitForm(event.currentTarget, "auth-message", async () => {
    const current = await request("auth/register", { method: "POST", body: {
      nombreEmpresa: $("#company-name").value.trim(), correo: $("#register-email").value.trim(),
      contrasenia: $("#register-password").value, telefono: $("#company-phone").value.trim(),
      telefonoOpcional: $("#company-phone-optional").value.trim() || null
    } });
    await refreshCsrf();
    await showSession(current);
  });
});

$("#company-list").addEventListener("click", async event => {
  const button = event.target.closest("button[data-company]");
  if (!button) return;
  const buttons = [...$("#company-list").querySelectorAll("button")];
  buttons.forEach(item => item.disabled = true);
  try {
    const current = await request("auth/company", { method: "POST", body: { empresaId: Number(button.dataset.company) } });
    await refreshCsrf();
    await showSession(current);
  } catch (error) { handleError("company-message", error); }
  finally { buttons.forEach(item => item.disabled = false); }
});

$("#logout").addEventListener("click", async event => {
  event.currentTarget.disabled = true;
  try {
    await request("auth/logout", { method: "POST" });
    showAuth();
  } catch (error) { handleError(session?.empresaActiva ? "message" : "company-message", error); }
  finally { $("#logout").disabled = false; }
});

async function loadProducts() {
  const version = viewVersion;
  $("#refresh").disabled = true;
  try {
    const [data, catalogs] = await Promise.all([request("products?incluirInactivos=true"), request("inventario/catalogos")]);
    if (version !== viewVersion) return;
    products = data;
    catalogos.set(catalogs);
    render();
    await inventario.changed(products);
  } catch (error) { if (version === viewVersion) handleError("message", error); }
  finally { if (version === viewVersion) $("#refresh").disabled = false; }
}

function render() {
  const search = $("#product-search").value.trim().toLocaleLowerCase();
  const status = $("#product-status").value;
  const category = $("#product-category-filter").value, warehouse = $("#product-warehouse-filter").value;
  $("#product-stock-heading").textContent = 'Disponible';
  const visible = products.filter(p => warehouse && `${p.code} ${p.name}`.toLocaleLowerCase().includes(search) &&
    (!category || p.categoriaId === Number(category)) && p.bodegaId === Number(warehouse) &&
    (status === "todos" || status === "inactivos" ? status === "todos" || !p.isActive : p.isActive && (status !== "bajos" || p.lowStock)));
  $("#empty").classList.toggle("hidden", visible.length !== 0);
  tbody.replaceChildren(...visible.map(product => {
    const row = document.createElement("tr");
    row.innerHTML = `
      <td>${escapeHtml(product.code)}</td><td class="product-name"><span class="product-name-text">${escapeHtml(product.name)}</span></td><td>${escapeHtml(product.unit)}</td>
      <td>${escapeHtml(catalogos.category(product.categoriaId))}</td><td>${catalogos.quantity(product.id, warehouse)}${product.lowStock ? '<br><span class="badge text-bg-warning">Stock bajo</span>' : ''}</td>
      <td>${new Intl.NumberFormat("es-GT", { style: "currency", currency: "GTQ" }).format(product.unitPrice)}</td>
      <td><div class="d-inline-flex gap-2">
        ${can("inventario.editar") ? `<button type="button" class="btn btn-outline-primary btn-sm" data-edit="${product.id}">Editar</button>` : ""}
        <button type="button" class="btn btn-outline-secondary btn-sm" data-stock="${product.id}">Movimientos</button>
        ${can("inventario.eliminar") ? `<button type="button" class="btn btn-outline-danger btn-sm" data-delete="${product.id}">Eliminar</button>` : ""}
      </div></td>`;
    return row;
  }));
  tablas.paginate("products");
}

form.addEventListener("submit", event => {
  event.preventDefault();
  const view = viewVersion;
  submitForm(form, "product-message", async current => {
    const id = fields.id.value;
    await request(id ? `products/${id}` : "products", { method: id ? "PUT" : "POST", body: {
      code: fields.code.value.trim(), name: fields.name.value.trim(),
      minimumStock: Number(fields.minimumStock.value), unitPrice: Number(fields.unitPrice.value), unit: fields.unit.value.trim(),
      allowsFractions: $("#product-fractions").value === "true", version: id ? Number(fields.version.value) : null,
      categoriaId: Number($("#product-category").value) || null, bodegaId: id ? null : Number($("#product-warehouse").value)
    } });
    if (!current()) return;
    resetForm();
    await dialogos.close('product-editor');
    if (view !== viewVersion) return;
    setMessage("message", id ? "Producto actualizado." : "Producto registrado.");
    await loadProducts();
  });
});

tbody.addEventListener("click", async event => {
  const button = event.target.closest("button");
  if (!button) return;
  if (button.dataset.edit && can("inventario.editar")) {
    const product = products.find(item => item.id === Number(button.dataset.edit));
    if (!product) return;
    Object.entries(fields).forEach(([key, input]) => input.value = product[key]);
    $("#product-fractions").value = String(product.allowsFractions);
    $("#product-category").value = product.categoriaId ?? '';
    $("#product-warehouse-field").classList.add('hidden'); $("#product-warehouse").required = false;
    setProductStep();
    $("#form-title").textContent = "Editar producto";
    clearMessage('product-message');
    await dialogos.open('product-editor', 'code');
  }
  if (button.dataset.stock) await inventario.open(Number(button.dataset.stock));
  if (button.dataset.delete && can("inventario.eliminar") &&
      confirm("¿Eliminar este producto y sus existencias de esta bodega? Su kárdex se conservará como historial de un producto eliminado.")) {
    button.disabled = true;
    const view = viewVersion;
    try {
      await request(`products/${button.dataset.delete}`, { method: 'DELETE' });
      if (view !== viewVersion) return;
      if (fields.id.value === button.dataset.delete) resetForm();
      setMessage("message", "Producto eliminado.");
      await loadProducts();
    } catch (error) { if (view === viewVersion) handleError("message", error); }
    finally { button.disabled = false; }
  }
});

async function loadUsers(messageId = "user-message", stillCurrent = () => true) {
  if (!isAdmin()) return;
  const version = viewVersion;
  try {
    const data = await request("usuarios");
    if (version !== viewVersion) return;
    users = data;
    $("#users").replaceChildren(...users.map(user => {
      const row = document.createElement("tr");
      row.innerHTML = `<td>${escapeHtml(user.correo)}</td><td>${escapeHtml(user.rol.nombre)}</td>
        <td>${new Intl.DateTimeFormat("es-GT", { dateStyle: "short" }).format(new Date(user.creadoEnUtc))}</td>
        <td><div class="d-flex gap-2">
          <button type="button" class="btn btn-outline-secondary btn-sm" data-view-user="${user.id}">Perfil</button>
          <button type="button" class="btn btn-outline-primary btn-sm" data-edit-user="${user.id}">Editar</button>
          <button type="button" class="btn btn-outline-danger btn-sm" data-delete-user="${user.id}">Eliminar</button>
        </div></td>`;
      return row;
    }));
    tablas.paginate("users");
  } catch (error) { if (version === viewVersion && stillCurrent()) handleError(messageId, error); }
}

$("#user-form").addEventListener("submit", event => {
  event.preventDefault();
  if (!isAdmin()) return;
  const correo = $("#user-email").value.trim();
  const contrasenia = $("#user-password").value;
  const rolId = Number($("#user-role").value);
  if (roles.find(rol => rol.id === rolId)?.esAdministrador &&
      !confirm("Este usuario tendrá control total de esta empresa. ¿Asignar Administrador de empresa?")) return;
  submitForm(event.currentTarget, "user-message", async current => {
    await request("usuarios", { method: "POST", body: {
      correo, contrasenia, rolId, telefono: $("#user-phone").value.trim() || null
    } });
    if (!current()) return;
    resetUserForm();
    await loadUsers();
    if (current()) setMessage("user-message", "Usuario creado.");
  });
});

function resetUserForm() {
  $("#user-form").reset();
  $("#user-password").value = "";
  $("#user-role").value = roles.find(rol => rol.codigoSistema === "OperadorInventario")?.id ?? "";
}

async function loadRoles() {
  const version = viewVersion;
  try {
    const data = await request("roles");
    if (version !== viewVersion) return;
    roles = data.roles;
    permissionCatalog = data.permisos;
    fillUserRoleChoices("#user-role", roles.find(rol => rol.codigoSistema === "OperadorInventario")?.id);
    if (userProfile) fillUserRoleChoices("#account-role", userProfile.rol.id);
    renderRolePermissions();
    $("#roles").replaceChildren(...roles.map(rol => {
      const row = document.createElement("tr");
      const names = rol.esAdministrador ? "Control total de esta empresa" :
        permissionCatalog.filter(permission => rol.permisos.includes(permission.clave)).map(permission => permission.nombre).join(", ") || "Sin permisos";
      row.innerHTML = `<td>${escapeHtml(rol.nombre)}</td><td><span class="badge text-bg-${rol.esSistema ? "secondary" : "primary"}">${rol.esSistema ? "Sistema" : "Empresa"}</span></td>
        <td class="text-wrap">${escapeHtml(names)}</td><td>${rol.esSistema ? "Protegido" :
          `<div class="d-flex gap-2"><button type="button" class="btn btn-outline-primary btn-sm" data-edit-role="${rol.id}">Editar</button><button type="button" class="btn btn-outline-danger btn-sm" data-delete-role="${rol.id}">Eliminar</button></div>`}</td>`;
      return row;
    }));
    tablas.paginate("roles");
  } catch (error) { if (version === viewVersion) handleError("role-message", error); }
}

function resetRoleForm() {
  $("#role-form").reset();
  selectedRolePermissions.clear();
  renderRolePermissions();
  $("#role-id").value = "";
  $("#role-form-title").textContent = "Crear rol";
  $("#cancel-role-edit").classList.add("hidden");
}

function renderRolePermissions() {
  const select = $("#role-permission-select");
  const previous = select.value;
  const available = permissionCatalog.filter(permission => !selectedRolePermissions.has(permission.clave));
  const placeholder = document.createElement("option");
  placeholder.value = ""; placeholder.textContent = "Seleccioná un permiso";
  select.replaceChildren(placeholder, ...available.map(permission => {
    const option = document.createElement("option");
    option.value = permission.clave; option.textContent = permission.nombre; return option;
  }));
  select.value = available.some(permission => permission.clave === previous) ? previous : "";
  select.disabled = available.length === 0;
  $("#role-permission-add").disabled = !select.value;
  $("#role-permissions").replaceChildren(...permissionCatalog.filter(permission => selectedRolePermissions.has(permission.clave)).map(permission => {
    const chip = document.createElement("span");
    chip.className = "badge text-bg-secondary d-inline-flex align-items-center gap-2 py-2";
    const label = document.createElement("span"); label.textContent = permission.nombre;
    const remove = document.createElement("button");
    remove.type = "button"; remove.className = "btn-close btn-close-white";
    remove.dataset.removePermission = permission.clave;
    remove.setAttribute("aria-label", `Quitar ${permission.nombre}`);
    chip.append(label, remove); return chip;
  }));
}

$("#role-permission-select").addEventListener("change", () => {
  $("#role-permission-add").disabled = !$("#role-permission-select").value;
});
$("#role-permission-add").addEventListener("click", () => {
  const key = $("#role-permission-select").value;
  if (!permissionCatalog.some(permission => permission.clave === key)) return;
  selectedRolePermissions.add(key);
  const view = `${key.split(".")[0]}.ver`;
  if (permissionCatalog.some(permission => permission.clave === view)) selectedRolePermissions.add(view);
  renderRolePermissions();
});
$("#role-permissions").addEventListener("click", event => {
  const button = event.target.closest("button[data-remove-permission]");
  if (!button) return;
  const key = button.dataset.removePermission;
  selectedRolePermissions.delete(key);
  if (key.endsWith(".ver")) {
    const prefix = key.slice(0, -3);
    for (const permission of selectedRolePermissions) if (permission.startsWith(prefix)) selectedRolePermissions.delete(permission);
  }
  renderRolePermissions();
});

$("#role-form").addEventListener("submit", event => {
  event.preventDefault();
  submitForm(event.currentTarget, "role-message", async () => {
    const id = $("#role-id").value;
    await request(id ? `roles/${id}` : "roles", { method: id ? "PUT" : "POST", body: {
      nombre: $("#role-name").value.trim(), permisos: permissionCatalog.filter(permission => selectedRolePermissions.has(permission.clave)).map(permission => permission.clave)
    } });
    resetRoleForm();
    await loadRoles();
    setMessage("role-message", id ? "Rol actualizado." : "Rol creado.");
  });
});

$("#roles").addEventListener("click", async event => {
  const button = event.target.closest("button");
  if (!button) return;
  if (button.dataset.editRole) {
    const rol = roles.find(item => item.id === Number(button.dataset.editRole));
    if (!rol || rol.esSistema) return;
    $("#role-id").value = rol.id;
    $("#role-name").value = rol.nombre;
    selectedRolePermissions.clear();
    rol.permisos.forEach(permission => selectedRolePermissions.add(permission));
    renderRolePermissions();
    $("#role-form-title").textContent = "Editar rol";
    $("#cancel-role-edit").classList.remove("hidden");
    clearMessage("role-message");
    $("#role-form").scrollIntoView({ behavior: "smooth", block: "center" });
  }
  if (button.dataset.deleteRole && confirm("¿Eliminar este rol? No se puede eliminar si tiene usuarios asignados.")) {
    button.disabled = true;
    try {
      await request(`roles/${button.dataset.deleteRole}`, { method: "DELETE" });
      resetRoleForm();
      await loadRoles();
      setMessage("role-message", "Rol eliminado.");
    } catch (error) { handleError("role-message", error); }
    finally { button.disabled = false; }
  }
});

$("#users").addEventListener("click", async event => {
  if (!isAdmin()) return;
  const button = event.target.closest("button");
  if (!button) return;
  const id = Number(button.dataset.viewUser ?? button.dataset.editUser ?? button.dataset.deleteUser);
  const user = users.find(item => item.id === id);
  if (!user) return;
  if (button.dataset.viewUser || button.dataset.editUser) {
    await openUserProfile(user.id, Boolean(button.dataset.editUser));
    return;
  }
  if (!button.dataset.deleteUser || !confirm(`¿Eliminar la cuenta ${user.correo} de esta empresa?`)) return;
  const ownAccount = id === session.usuario.id;
  const version = viewVersion;
  button.disabled = true;
  clearMessage("user-message");
  try {
    await request(`usuarios/${id}`, { method: "DELETE" });
    if (version !== viewVersion) return;
    if (ownAccount) {
      showAuth();
      setMessage("auth-message", "Tu cuenta fue eliminada de esta empresa.");
      return;
    }
    if (userProfile?.id === id) { resetUserProfile(); await dialogos.close("user-profile-modal"); }
    await loadUsers();
    setMessage("user-message", "Usuario eliminado.");
  } catch (error) { handleError("user-message", error); }
  finally { button.disabled = false; }
});

$("#cancel-role-edit").addEventListener("click", resetRoleForm);

function fillUserRoleChoices(selector, fallbackId) {
  const select = $(selector), previous = String(select.value);
  const available = userProfile && !roles.some(rol => rol.id === userProfile.rol.id) ? [...roles, userProfile.rol] : roles;
  select.replaceChildren(...available.map(rol => {
    const option = document.createElement("option"); option.value = String(rol.id);
    option.textContent = `${rol.nombre} (${rol.esSistema ? "Sistema" : "Empresa"})`; return option;
  }));
  select.value = available.some(rol => String(rol.id) === previous) ? previous : String(fallbackId ?? available[0]?.id ?? "");
}
function setUserProfileEditing(editing) {
  editingUserProfile = Boolean(editing && userProfile && isAdmin());
  $("#user-profile-title").textContent = editingUserProfile ? "Editar usuario" : "Perfil de usuario";
  $("#user-profile-summary").classList.toggle("hidden", editingUserProfile);
  $("#user-profile-form").classList.toggle("hidden", !editingUserProfile);
  $("#user-profile-edit").classList.toggle("hidden", !userProfile || editingUserProfile || !isAdmin());
  ["email", "phone", "role", "password", "password-confirmation"].forEach(field => $("#account-" + field).disabled = !editingUserProfile);
  syncPasswordConfirmation("account");
}
function resetUserProfile() {
  userProfile = null; userProfileVersion++;
  $("#user-profile-form").reset();
  ["email", "phone", "password", "password-confirmation"].forEach(field => $("#account-" + field).value = "");
  $("#account-role").replaceChildren();
  ["id", "company", "created", "email", "phone", "role", "avatar"].forEach(field => $("#user-profile-" + field).textContent = "");
  $("#user-profile-permissions").classList.remove("user-profile-full-access");
  $("#user-profile-permissions").replaceChildren();
  $("#user-profile-content").classList.add("hidden"); $("#user-profile-loading").classList.add("hidden");
  setUserProfileEditing(false); clearMessage("user-profile-message");
}
function fillUserProfile(user) {
  userProfile = user;
  $("#user-profile-id").textContent = String(user.id);
  $("#user-profile-company").textContent = session.empresaActiva.nombre;
  $("#user-profile-created").textContent = new Intl.DateTimeFormat("es-GT", { dateStyle: "medium", timeStyle: "short" }).format(new Date(user.creadoEnUtc));
  $("#user-profile-email").textContent = user.correo; $("#user-profile-phone").textContent = user.telefono ?? "Sin registrar";
  const initials = user.correo.split("@")[0].match(/[\p{L}\p{N}]/gu) ?? [];
  $("#user-profile-avatar").textContent = initials.slice(0, 2).join("").toLocaleUpperCase("es-GT");
  $("#user-profile-role").textContent = `${user.rol.nombre} (${user.rol.esSistema ? "Sistema" : "Empresa"})`;
  $("#user-profile-permissions").classList.toggle("user-profile-full-access", user.rol.esAdministrador);
  const permissions = user.rol.esAdministrador ? ["Control total de esta empresa"] : user.rol.permisos.map(key => permissionCatalog.find(item => item.clave === key)?.nombre ?? key);
  $("#user-profile-permissions").replaceChildren(...(permissions.length ? permissions : ["Sin permisos"]).map(name => {
    const item = document.createElement("li"); item.textContent = name; return item;
  }));
  $("#account-email").value = user.correo; $("#account-phone").value = user.telefono ?? "";
  $("#account-role").value = ""; fillUserRoleChoices("#account-role", user.rol.id);
  $("#account-password").value = ""; $("#account-password-confirmation").value = "";
  $("#user-profile-content").classList.remove("hidden"); setUserProfileEditing(false);
}
async function openUserProfile(id, editing = false) {
  if (!isAdmin()) return;
  resetUserProfile();
  const view = viewVersion, revision = userProfileVersion;
  $("#user-profile-loading").classList.remove("hidden");
  await dialogos.open("user-profile-modal", "user-profile-title");
  const generation = dialogos.version("user-profile-modal");
  const current = () => view === viewVersion && revision === userProfileVersion && isAdmin() &&
    generation === dialogos.version("user-profile-modal") && $("#user-profile-modal").classList.contains("show");
  if (!current()) return;
  try {
    const user = await request(`usuarios/${id}`);
    if (!current()) return;
    fillUserProfile(user); setUserProfileEditing(editing);
    if (editingUserProfile) $("#account-email").focus();
  } catch (error) { if (current()) handleError("user-profile-message", error); }
  finally { if (current()) $("#user-profile-loading").classList.add("hidden"); }
}
$("#user-profile-modal").addEventListener("hide.bs.modal", resetUserProfile);
$("#user-profile-edit").addEventListener("click", () => { if (userProfile && isAdmin()) { userProfileVersion++; setUserProfileEditing(true); $("#account-email").focus(); } });
$("#user-profile-cancel").addEventListener("click", () => { if (userProfile) { userProfileVersion++; fillUserProfile(userProfile); clearMessage("user-profile-message"); $("#user-profile-edit").focus(); } });
$("#user-profile-form").addEventListener("submit", event => {
  event.preventDefault();
  if (!isAdmin() || !userProfile || !editingUserProfile) return;
  if (!syncPasswordConfirmation("account")) { $("#account-password-confirmation").reportValidity(); return; }
  const id = userProfile.id, revision = userProfileVersion;
  const correo = $("#account-email").value.trim(), contrasenia = $("#account-password").value, rolId = Number($("#account-role").value);
  if (roles.find(rol => rol.id === rolId)?.esAdministrador && !userProfile.rol.esAdministrador &&
      !confirm("Este usuario tendrá control total de esta empresa. ¿Asignar Administrador de empresa?")) return;
  const ownAccount = id === session.usuario.id;
  const credentialsChanged = contrasenia !== "" || correo.toUpperCase() !== userProfile.correo.toUpperCase();
  const sameProfile = () => userProfile?.id === id && userProfileVersion === revision;
  submitForm(event.currentTarget, "user-profile-message", async current => {
    const updated = await request(`usuarios/${id}`, { method: "PUT", body: {
      correo, contrasenia: contrasenia || null, confirmacionContrasenia: $("#account-password-confirmation").value || null,
      rolId, telefono: $("#account-phone").value.trim() || null
    } });
    if (!current() || !sameProfile()) return;
    if (ownAccount && credentialsChanged) { showAuth(); setMessage("auth-message", "Datos actualizados. Iniciá sesión nuevamente."); return; }
    if (ownAccount) {
      if (!updated.rol.esAdministrador) {
        const me = await request("auth/me"); if (!current() || !sameProfile()) return;
        await showSession(me); return;
      }
      session.usuario = updated; session.empresaActiva.rol = updated.rol; fillProfile(updated);
    }
    fillUserProfile(updated); await loadUsers("user-profile-message", () => current() && sameProfile());
    if (current() && sameProfile()) setMessage("user-profile-message", "Usuario actualizado.");
  }, sameProfile);
});

function resetForm() {
  form.reset();
  fields.id.value = "";
  fields.version.value = "";
  $("#product-warehouse-field").classList.remove('hidden'); $("#product-warehouse").required = true;
  $("#product-fractions").value = "false";
  fields.unit.value = "Unidad";
  setProductStep();
  $("#form-title").textContent = "Nuevo producto";
  clearMessage('product-message');
  if (catalogos.selected()) $('#product-warehouse').value = String(catalogos.selected());
}
function setProductStep() {
  const step = $("#product-fractions").value === "true" ? "0.001" : "1";
  fields.minimumStock.step = step;
}
$("#product-fractions").addEventListener("change", setProductStep);
$("#product-search").addEventListener("input", render);
$("#product-status").addEventListener("change", render);
$("#product-category-filter").addEventListener("change", render);
$("#product-warehouse-filter").addEventListener("change", render);
function setMessage(id, text, isError = false) {
  $("#" + id).textContent = text;
  $("#" + id).className = `alert alert-${isError ? "danger" : "success"} mt-3 mb-0`;
}
function clearMessage(id) {
  $("#" + id).textContent = "";
  $("#" + id).className = "alert d-none mt-3 mb-0";
}
function escapeHtml(value) {
  const element = document.createElement("span");
  element.textContent = value;
  return element.innerHTML;
}

function syncPasswordConfirmation(prefix) {
  const password = $(`#${prefix}-password`), confirmation = $(`#${prefix}-password-confirmation`);
  confirmation.required = !confirmation.disabled && Boolean(password.value || confirmation.value);
  $(`#${prefix}-password-confirmation-label`).textContent = "Confirmar nueva contraseña" + (confirmation.required ? " *" : "");
  const matches = confirmation.disabled || password.value === confirmation.value;
  confirmation.setCustomValidity(matches ? "" : "Las contraseñas no coinciden.");
  return matches;
}
function resetProfilePassword(admin = false) {
  $("#profile-password-fields").classList.toggle("hidden", !admin);
  $("#profile-password").value = ""; $("#profile-password").disabled = !admin;
  $("#profile-password-confirmation").value = ""; $("#profile-password-confirmation").disabled = !admin;
  syncPasswordConfirmation("profile");
}
["profile", "account"].forEach(prefix => {
  ["password", "password-confirmation"].forEach(field =>
    $(`#${prefix}-${field}`).addEventListener("input", () => syncPasswordConfirmation(prefix)));
});

function fillProfile(user) {
  $("#profile-email").value = user.correo;
  $("#profile-phone").value = user.telefono ?? "";
  resetProfilePassword(isAdmin() && user.rol.esAdministrador);
  $("#profile-company").textContent = session.empresaActiva.nombre;
  $("#profile-role").textContent = user.rol.nombre;
  $("#profile-created").textContent = new Intl.DateTimeFormat("es-GT", { dateStyle: "short" }).format(new Date(user.creadoEnUtc));
  clearMessage("profile-message");
}

$("#profile-tab").addEventListener("click", async () => {
  if (!session?.empresaActiva) return;
  showTab("profile");
  const version = viewVersion;
  try {
    const user = await request("perfil");
    if (version === viewVersion) fillProfile(user);
  } catch (error) { if (version === viewVersion) handleError("profile-message", error); }
});

$("#profile-form").addEventListener("submit", event => {
  event.preventDefault();
  if (!session?.empresaActiva) return;
  if (isAdmin() && !syncPasswordConfirmation("profile")) { $("#profile-password-confirmation").reportValidity(); return; }
  const correo = $("#profile-email").value.trim();
  const contrasenia = isAdmin() ? $("#profile-password").value : "";
  const credentialsChanged = contrasenia !== "" || correo.toUpperCase() !== session.usuario.correo.toUpperCase();
  const version = viewVersion;
  submitForm(event.currentTarget, "profile-message", async () => {
    const updated = await request("perfil", { method: "PUT", body: {
      correo, contrasenia: contrasenia || null, telefono: $("#profile-phone").value.trim() || null,
      confirmacionContrasenia: isAdmin() ? $("#profile-password-confirmation").value || null : null
    } });
    if (version !== viewVersion) return;
    if (credentialsChanged) {
      showAuth();
      setMessage("auth-message", "Perfil actualizado. Iniciá sesión nuevamente.");
      return;
    }
    session.usuario = updated;
    fillProfile(updated);
    setMessage("profile-message", "Perfil actualizado.");
  });
});

$("#show-register").addEventListener("click", () => toggleAuth(true));
$("#show-login").addEventListener("click", () => toggleAuth(false));
$("#change-company").addEventListener("click", showCompanyChooser);
$("#inventory-tab").addEventListener("click", () => { if (can("inventario.ver")) showTab("inventory"); });
$("#users-tab").addEventListener("click", async () => { if (!isAdmin()) return; showTab("users"); await loadRoles(); await loadUsers(); });
$("#roles-tab").addEventListener("click", () => { if (!isAdmin()) return; showTab("roles"); loadRoles(); });
$("#cancel-edit").addEventListener("click", resetForm);
$('#product-new').addEventListener('click', () => {
  if (!can('inventario.crear') || !catalogos.selected()) return;
  resetForm(); dialogos.open('product-editor', 'code');
});
$("#refresh").addEventListener("click", () => { clearMessage("message"); loadProducts(); });
$("#project-document-link").addEventListener("click", event => {
  if (event.currentTarget.getAttribute("aria-disabled") === "true") event.preventDefault();
});
initialize();
