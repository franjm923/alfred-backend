document.querySelectorAll('.btn.aceptar').forEach(button => {
  button.addEventListener('click', (e) => {
    selectedRow = e.target.closest('tr'); // fila del pedido
    document.getElementById('modal-envio').style.display = 'flex';
  });
});

document.getElementById('modal-cancelar').addEventListener('click', () => {
  document.getElementById('modal-envio').style.display = 'none';
  limpiarModal();
});

document.getElementById('modal-confirmar').addEventListener('click', () => {
  const precioEnvio = document.getElementById('precio-envio').value.trim();
  const precioTotal = document.getElementById('precio-total').value.trim();

  if(precioEnvio === '' || precioTotal === '') {
    alert('Por favor, completá ambos campos.');
    return;
  }

  // Actualizo estado de la fila
  const estadoCell = selectedRow.querySelector('td:nth-child(4)');
  estadoCell.innerHTML = `<span class="badge aceptado">Aceptado</span><br>Envio: $${precioEnvio}<br>Total: $${precioTotal}`;

  // Opcional: mover fila a otra tabla o actualizar datos reales

  document.getElementById('modal-envio').style.display = 'none';
  limpiarModal();
});

function limpiarModal() {
  document.getElementById('precio-envio').value = '';
  document.getElementById('precio-total').value = '';
}

let selectedRow = null;