// Simulamos datos de pedidos entregados (esto puede venir de API o localStorage)
const pedidosEntregados = [
  {
    id: 101,
    producto: "Vape X",
    cantidad: 2,
    direccion: "Calle Falsa 123",
    medioPago: "Tarjeta",
    estado: "Entregado"
  },
  {
    id: 102,
    producto: "E-liquid 50ml",
    cantidad: 1,
    direccion: "Av. Siempre Viva 742",
    medioPago: "Efectivo",
    estado: "Entregado"
  },
  {
    id: 103,
    producto: "Vape Y",
    cantidad: 3,
    direccion: "Pje. Los Álamos 555",
    medioPago: "Tarjeta",
    estado: "Entregado"
  }
];

// Función para mostrar los pedidos en la tabla
function mostrarPedidos() {
  const tbody = document.getElementById('pedidos-body');
  tbody.innerHTML = ''; // limpio

  pedidosEntregados.forEach(pedido => {
    const tr = document.createElement('tr');

    tr.innerHTML = `
      <td>#${pedido.id}</td>
      <td>${pedido.producto}</td>
      <td>${pedido.cantidad}</td>
      <td>${pedido.direccion}</td>
      <td>${pedido.medioPago}</td>
      <td><span class="badge entregado">${pedido.estado}</span></td>
    `;

    tbody.appendChild(tr);
  });
}

// Ejecutar al cargar la página
window.onload = mostrarPedidos;