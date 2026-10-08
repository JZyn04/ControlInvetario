const dialogos = (() => {
  let revision = 0;
  let queue = Promise.resolve();
  const states = new WeakMap(), openers = new WeakMap(), generations = new WeakMap();
  const element = id => document.getElementById(id);
  const waitFor = (node, name) => new Promise(resolve => node.addEventListener(name, resolve, { once: true }));
  for (const node of document.querySelectorAll('.modal')) {
    states.set(node, 'closed');
    node.addEventListener('show.bs.modal', () => { states.set(node, 'opening'); generations.set(node, (generations.get(node) ?? 0) + 1); });
    node.addEventListener('shown.bs.modal', () => states.set(node, 'open'));
    node.addEventListener('hide.bs.modal', () => { states.set(node, 'closing'); generations.set(node, (generations.get(node) ?? 0) + 1); });
    node.addEventListener('hidden.bs.modal', () => {
      states.set(node, 'closed');
      const opener = openers.get(node);
      if (opener?.isConnected && opener.getClientRects().length) opener.focus();
    });
  }
  function enqueue(action) {
    const next = queue.then(action); queue = next.catch(() => {}); return next;
  }
  async function closeNode(node) {
    if (!node) return;
    if (states.get(node) === 'opening') await waitFor(node, 'shown.bs.modal');
    if (states.get(node) === 'closing') { await waitFor(node, 'hidden.bs.modal'); return; }
    if (!node.classList.contains('show')) return;
    const hidden = waitFor(node, 'hidden.bs.modal');
    bootstrap.Modal.getOrCreateInstance(node).hide(); await hidden;
  }
  const close = id => enqueue(() => closeNode(element(id)));
  function open(id, focusId) {
    const ticket = ++revision, opener = document.activeElement;
    return enqueue(async () => {
      if (ticket !== revision) return;
      const node = element(id);
      for (const modal of document.querySelectorAll('.modal')) if (modal !== node) await closeNode(modal);
      if (ticket !== revision) return;
      openers.set(node, opener);
      if (states.get(node) === 'closing') await waitFor(node, 'hidden.bs.modal');
      if (!node.classList.contains('show')) {
        const shown = waitFor(node, 'shown.bs.modal');
        bootstrap.Modal.getOrCreateInstance(node).show(); await shown;
      }
      if (ticket === revision && focusId) element(focusId)?.focus();
    });
  }
  function reset() {
    revision++;
    return enqueue(async () => { for (const node of document.querySelectorAll('.modal')) await closeNode(node); });
  }
  return { open, close, reset, version: id => generations.get(element(id)) ?? 0 };
})();
