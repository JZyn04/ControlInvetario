const localApiByPort = {
  "5070": "http://localhost:5279",
  "7219": "https://localhost:7201"
};
const apiBaseUrl = localApiByPort[window.location.port] ?? window.location.origin;
const api = `${apiBaseUrl}/api/products`;
const form = document.querySelector("#product-form");
const message = document.querySelector("#message");
const tbody = document.querySelector("#products");
const empty = document.querySelector("#empty");
const cancelEdit = document.querySelector("#cancel-edit");

const fields = {
  id: document.querySelector("#product-id"),
  code: document.querySelector("#code"),
  name: document.querySelector("#name"),
  quantity: document.querySelector("#quantity"),
  minimumStock: document.querySelector("#minimum-stock"),
  unitPrice: document.querySelector("#unit-price")
};

let products = [];

async function loadProducts() {
  try {
    const response = await fetch(api);
    if (!response.ok) throw new Error("No se pudo consultar el inventario.");
    products = await response.json();
    render();
  } catch (error) {
    setMessage(error.message, true);
  }
}

function render() {
  document.querySelector("#product-count").textContent = products.length;
  empty.classList.toggle("hidden", products.length !== 0);
  tbody.replaceChildren(...products.map(product => {
    const row = document.createElement("tr");
    row.innerHTML = `
      <td>${escapeHtml(product.code)}</td>
      <td>${escapeHtml(product.name)}</td>
      <td>${product.quantity}</td>
      <td>${product.minimumStock}</td>
      <td>${new Intl.NumberFormat("es-GT", { style: "currency", currency: "GTQ" }).format(product.unitPrice)}</td>
      <td><span class="status ${product.lowStock ? "low" : "ok"}">${product.lowStock ? "Stock bajo" : "Disponible"}</span></td>
      <td class="actions">
        <button class="button secondary" data-edit="${product.id}">Editar</button>
        <button class="button danger" data-delete="${product.id}">Eliminar</button>
      </td>`;
    return row;
  }));
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const id = fields.id.value;
  const payload = {
    code: fields.code.value,
    name: fields.name.value,
    quantity: Number(fields.quantity.value),
    minimumStock: Number(fields.minimumStock.value),
    unitPrice: Number(fields.unitPrice.value)
  };

  try {
    const response = await fetch(id ? `${api}/${id}` : api, {
      method: id ? "PUT" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (!response.ok) throw new Error(await readError(response));
    resetForm();
    setMessage(id ? "Producto actualizado." : "Producto registrado.");
    await loadProducts();
  } catch (error) {
    setMessage(error.message, true);
  }
});

tbody.addEventListener("click", async event => {
  const editId = event.target.dataset.edit;
  const deleteId = event.target.dataset.delete;

  if (editId) {
    const product = products.find(item => item.id === Number(editId));
    if (!product) return;
    Object.entries(fields).forEach(([key, input]) => input.value = product[key]);
    document.querySelector("#form-title").textContent = "Editar producto";
    cancelEdit.classList.remove("hidden");
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  if (deleteId && confirm("¿Eliminar este producto del inventario?")) {
    const response = await fetch(`${api}/${deleteId}`, { method: "DELETE" });
    if (response.ok) {
      setMessage("Producto eliminado.");
      await loadProducts();
    } else {
      setMessage("No se pudo eliminar el producto.", true);
    }
  }
});

function resetForm() {
  form.reset();
  fields.id.value = "";
  fields.quantity.value = "0";
  fields.minimumStock.value = "0";
  fields.unitPrice.value = "0";
  document.querySelector("#form-title").textContent = "Nuevo producto";
  cancelEdit.classList.add("hidden");
}

function setMessage(text, isError = false) {
  message.textContent = text;
  message.style.color = isError ? "#b42318" : "#0d4f66";
}

async function readError(response) {
  const body = await response.json().catch(() => null);
  return body?.title ?? "La operación no pudo completarse.";
}

function escapeHtml(value) {
  const element = document.createElement("span");
  element.textContent = value;
  return element.innerHTML;
}

cancelEdit.addEventListener("click", resetForm);
document.querySelector("#refresh").addEventListener("click", loadProducts);
loadProducts();
