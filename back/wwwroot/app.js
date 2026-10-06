const localApiByPort = { "5070": "http://localhost:5279", "7219": "https://localhost:7201" };
const apiBaseUrl = localApiByPort[window.location.port] ?? window.location.origin;
const $ = selector => document.querySelector(selector);
const form = $("#product-form");
const tbody = $("#products");
const fields = {
  id: $("#product-id"), code: $("#code"), name: $("#name"), quantity: $("#quantity"),
  minimumStock: $("#minimum-stock"), unitPrice: $("#unit-price")
};
let session = null;
let csrfToken = null;
let products = [];
let roles = [];
let permissionCatalog = [];
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
  session = null;
  csrfToken = null;
  viewVersion++;
  products = [];
  roles = [];
  tbody.replaceChildren();
  $("#users").replaceChildren();
  resetForm();
  $("#login-form").reset();
  $("#register-form").reset();
  $("#user-form").reset();
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
  session = current;
  viewVersion++;
  products = [];
  roles = [];
  tbody.replaceChildren();
  $("#users").replaceChildren();
  $("#product-count").textContent = "0";
  $("#auth-panel").classList.add("hidden");
  $("#session-bar").classList.remove("hidden");
  $("#session-email").textContent = current.usuario.correo;
  $("#change-company").classList.toggle("hidden", current.empresas.length < 2);
  $("#login-form").reset();
  $("#register-form").reset();
  $("#user-form").reset();
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
  $("#empresa-contacto").textContent = [current.empresaActiva.telefono, current.empresaActiva.telefonoOpcional].filter(Boolean).join(" · ");
  $("#session-role").textContent = current.empresaActiva.rol.nombre;
  $("#inventory-tab").classList.toggle("hidden", !can("inventario.ver"));
  $("#product-total").classList.toggle("hidden", !can("inventario.ver"));
  $("#no-access").classList.toggle("hidden", can("inventario.ver"));
  $("#users-tab").classList.toggle("hidden", !isAdmin());
  $("#roles-tab").classList.toggle("hidden", !isAdmin());
  showTab(can("inventario.ver") ? "inventory" : null);
  resetForm();
  if (isAdmin()) await loadRoles();
  if (can("inventario.ver")) await loadProducts();
}

function showCompanyChooser() {
  viewVersion++;
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
  ["inventory", "users", "roles"].forEach(name => {
    $("#" + name + "-panel").classList.toggle("hidden", selected !== name);
    const tab = $("#" + name + "-tab");
    const active = name === selected;
    tab.classList.toggle("active", active);
    tab.setAttribute("aria-selected", String(active));
  });
}

async function submitForm(target, messageId, action) {
  const button = target.querySelector('button[type="submit"]');
  button.disabled = true;
  clearMessage(messageId);
  try { await action(); }
  catch (error) { handleError(messageId, error); }
  finally { button.disabled = false; }
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
    const data = await request("products");
    if (version !== viewVersion) return;
    products = data;
    render();
  } catch (error) { if (version === viewVersion) handleError("message", error); }
  finally { if (version === viewVersion) $("#refresh").disabled = false; }
}

function render() {
  $("#product-count").textContent = products.length;
  $("#empty").classList.toggle("hidden", products.length !== 0);
  tbody.replaceChildren(...products.map(product => {
    const row = document.createElement("tr");
    row.innerHTML = `
      <td>${escapeHtml(product.code)}</td><td>${escapeHtml(product.name)}</td>
      <td>${product.quantity}</td><td>${product.minimumStock}</td>
      <td>${new Intl.NumberFormat("es-GT", { style: "currency", currency: "GTQ" }).format(product.unitPrice)}</td>
      <td><span class="badge text-bg-${product.lowStock ? "warning" : "success"}">${product.lowStock ? "Stock bajo" : "Disponible"}</span></td>
      <td class="text-end"><div class="d-inline-flex gap-2">
        ${can("inventario.editar") ? `<button type="button" class="btn btn-outline-primary btn-sm" data-edit="${product.id}">Editar</button>` : ""}
        ${can("inventario.eliminar") ? `<button type="button" class="btn btn-outline-danger btn-sm" data-delete="${product.id}">Eliminar</button>` : ""}
      </div></td>`;
    return row;
  }));
}

form.addEventListener("submit", event => {
  event.preventDefault();
  submitForm(form, "message", async () => {
    const id = fields.id.value;
    await request(id ? `products/${id}` : "products", { method: id ? "PUT" : "POST", body: {
      code: fields.code.value.trim(), name: fields.name.value.trim(), quantity: Number(fields.quantity.value),
      minimumStock: Number(fields.minimumStock.value), unitPrice: Number(fields.unitPrice.value)
    } });
    resetForm();
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
    $("#form-title").textContent = "Editar producto";
    $("#cancel-edit").classList.remove("hidden");
    $("#product-editor").classList.remove("hidden");
    form.scrollIntoView({ behavior: "smooth", block: "center" });
  }
  if (button.dataset.delete && can("inventario.eliminar") && confirm("¿Eliminar este producto del inventario?")) {
    button.disabled = true;
    try {
      await request(`products/${button.dataset.delete}`, { method: "DELETE" });
      if (fields.id.value === button.dataset.delete) resetForm();
      setMessage("message", "Producto eliminado.");
      await loadProducts();
    } catch (error) { handleError("message", error); }
    finally { button.disabled = false; }
  }
});

async function loadUsers() {
  const version = viewVersion;
  try {
    const users = await request("usuarios");
    if (version !== viewVersion) return;
    $("#users").replaceChildren(...users.map(user => {
      const row = document.createElement("tr");
      row.innerHTML = `<td>${escapeHtml(user.correo)}</td><td>${escapeHtml(user.rol.nombre)}</td>
        <td>${new Intl.DateTimeFormat("es-GT", { dateStyle: "short" }).format(new Date(user.creadoEnUtc))}</td>
        <td><div class="d-flex gap-2"><select class="form-select form-select-sm" aria-label="Rol de ${escapeHtml(user.correo)}" data-user-role="${user.id}">
          ${roles.map(rol => `<option value="${rol.id}" ${rol.id === user.rol.id ? "selected" : ""}>${escapeHtml(rol.nombre)} (${rol.esSistema ? "Sistema" : "Empresa"})</option>`).join("")}
        </select><button type="button" class="btn btn-outline-primary btn-sm" data-assign="${user.id}">Guardar</button></div></td>`;
      return row;
    }));
  } catch (error) { if (version === viewVersion) handleError("user-message", error); }
}

$("#user-form").addEventListener("submit", event => {
  event.preventDefault();
  submitForm(event.currentTarget, "user-message", async () => {
    await request("usuarios", { method: "POST", body: {
      correo: $("#user-email").value.trim(), contrasenia: $("#user-password").value, rolId: Number($("#user-role").value)
    } });
    $("#user-form").reset();
    setMessage("user-message", "Usuario agregado.");
    await loadUsers();
  });
});

async function loadRoles() {
  const version = viewVersion;
  try {
    const data = await request("roles");
    if (version !== viewVersion) return;
    roles = data.roles;
    permissionCatalog = data.permisos;
    const selectedRole = $("#user-role").value;
    $("#user-role").replaceChildren(...roles.map(rol => {
      const option = document.createElement("option");
      option.value = rol.id;
      option.textContent = `${rol.nombre} (${rol.esSistema ? "Sistema" : "Empresa"})`;
      option.defaultSelected = rol.esSistema && !rol.esAdministrador;
      return option;
    }));
    $("#user-role").value = roles.some(rol => String(rol.id) === selectedRole) ? selectedRole :
      (roles.find(rol => rol.esSistema && !rol.esAdministrador)?.id ?? roles[0]?.id ?? "");
    const checked = [...$("#role-permissions").querySelectorAll("input:checked")].map(input => input.value);
    $("#role-permissions").innerHTML = permissionCatalog.map((permission, index) =>
      `<div class="form-check"><input class="form-check-input" type="checkbox" id="permission-${index}" value="${escapeHtml(permission.clave)}" ${checked.includes(permission.clave) ? "checked" : ""}>
       <label class="form-check-label" for="permission-${index}">${escapeHtml(permission.nombre)}</label></div>`).join("");
    $("#roles").replaceChildren(...roles.map(rol => {
      const row = document.createElement("tr");
      const names = rol.esAdministrador ? "Control total de esta empresa" :
        permissionCatalog.filter(permission => rol.permisos.includes(permission.clave)).map(permission => permission.nombre).join(", ") || "Sin permisos";
      row.innerHTML = `<td>${escapeHtml(rol.nombre)}</td><td><span class="badge text-bg-${rol.esSistema ? "secondary" : "primary"}">${rol.esSistema ? "Sistema" : "Empresa"}</span></td>
        <td class="text-wrap">${escapeHtml(names)}</td><td>${rol.esSistema ? "Protegido" :
          `<div class="d-flex gap-2"><button type="button" class="btn btn-outline-primary btn-sm" data-edit-role="${rol.id}">Editar</button><button type="button" class="btn btn-outline-danger btn-sm" data-delete-role="${rol.id}">Eliminar</button></div>`}</td>`;
      return row;
    }));
  } catch (error) { if (version === viewVersion) handleError("role-message", error); }
}

function resetRoleForm() {
  $("#role-form").reset();
  $("#role-id").value = "";
  $("#role-form-title").textContent = "Crear rol";
  $("#cancel-role-edit").classList.add("hidden");
}

$("#role-permissions").addEventListener("change", event => {
  const view = $("#role-permissions").querySelector('input[value="inventario.ver"]');
  if (event.target !== view && event.target.checked) view.checked = true;
  if (event.target === view && !view.checked)
    $("#role-permissions").querySelectorAll("input").forEach(input => input.checked = false);
});

$("#role-form").addEventListener("submit", event => {
  event.preventDefault();
  submitForm(event.currentTarget, "role-message", async () => {
    const id = $("#role-id").value;
    await request(id ? `roles/${id}` : "roles", { method: id ? "PUT" : "POST", body: {
      nombre: $("#role-name").value.trim(), permisos: [...$("#role-permissions").querySelectorAll("input:checked")].map(input => input.value)
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
    $("#role-permissions").querySelectorAll("input").forEach(input => input.checked = rol.permisos.includes(input.value));
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
  const button = event.target.closest("button[data-assign]");
  if (!button) return;
  const rolId = Number($("#users").querySelector(`select[data-user-role="${button.dataset.assign}"]`).value);
  const rol = roles.find(item => item.id === rolId);
  if (rol?.esAdministrador && !confirm("Este usuario tendrá control total de esta empresa, incluidos usuarios y roles. ¿Asignar Administrador de empresa?")) return;
  button.disabled = true;
  clearMessage("user-message");
  try {
    await request(`usuarios/${button.dataset.assign}/rol`, { method: "PUT", body: { rolId } });
    if (Number(button.dataset.assign) === session.usuario.id) {
      const current = await request("auth/me");
      await showSession(current);
      if (!isAdmin()) return;
      showTab("users");
    }
    await loadUsers();
    setMessage("user-message", "Rol asignado.");
  } catch (error) { handleError("user-message", error); }
  finally { button.disabled = false; }
});

$("#cancel-role-edit").addEventListener("click", resetRoleForm);

function resetForm() {
  form.reset();
  fields.id.value = "";
  $("#form-title").textContent = "Nuevo producto";
  $("#cancel-edit").classList.add("hidden");
  $("#product-editor").classList.toggle("hidden", !can("inventario.crear"));
}
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

$("#show-register").addEventListener("click", () => toggleAuth(true));
$("#show-login").addEventListener("click", () => toggleAuth(false));
$("#change-company").addEventListener("click", showCompanyChooser);
$("#inventory-tab").addEventListener("click", () => { if (can("inventario.ver")) showTab("inventory"); });
$("#users-tab").addEventListener("click", async () => { if (!isAdmin()) return; showTab("users"); await loadRoles(); await loadUsers(); });
$("#roles-tab").addEventListener("click", () => { if (!isAdmin()) return; showTab("roles"); loadRoles(); });
$("#cancel-edit").addEventListener("click", resetForm);
$("#refresh").addEventListener("click", () => { clearMessage("message"); loadProducts(); });
initialize();
