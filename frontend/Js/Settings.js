// Js/Settings.js
// Página de configuración: estado y conexión de Google Calendar.

function logout() {
  Alfred.cerrarSesion();
}

function conectarCalendar() {
  // Navegación top-level al backend: lleva la cookie de sesión automáticamente.
  window.location.href = `${Alfred.API_BASE}/calendar/connect`;
}

function pintarUsuario(usuario) {
  Alfred.setText("nombreUsuario", usuario.name || usuario.email);
  Alfred.setSrc("avatar", usuario.picture);
}

function formatearFecha(iso) {
  if (!iso) return "fecha desconocida";
  return new Date(iso).toLocaleDateString("es-AR", {
    day: "2-digit",
    month: "long",
    year: "numeric",
  });
}

function vistaConectado(estado) {
  return `
    <p>✅ <strong>Conectado</strong></p>
    <p style="color: var(--muted, #6b7280);">📧 ${estado.email ?? ""}</p>
    <p style="color: var(--muted, #6b7280);">🕐 Conectado el ${formatearFecha(estado.connectedAt)}</p>
    <button class="btn btn-secondary" id="btn-calendar" style="margin-top: 12px;">Reconectar</button>
  `;
}

function vistaDesconectado() {
  return `
    <p>⚪ <strong>No conectado</strong></p>
    <p style="color: var(--muted, #6b7280);">
      Conectá tu Google Calendar para que Alfred consulte tu disponibilidad automáticamente.
    </p>
    <button class="btn btn-primary" id="btn-calendar" style="margin-top: 12px;">Conectar Google Calendar</button>
  `;
}

function vistaError() {
  return `
    <p>⚠️ <strong>No pudimos verificar el estado</strong></p>
    <p style="color: var(--muted, #6b7280);">Revisá tu conexión e intentá de nuevo.</p>
    <button class="btn btn-secondary" id="btn-reintentar" style="margin-top: 12px;">Reintentar</button>
  `;
}

async function cargarEstadoCalendar() {
  const contenedor = document.getElementById("calendar-status");
  if (!contenedor) return;
  try {
    const estado = await Alfred.apiGet("/api/calendar/status");
    contenedor.innerHTML = estado.connected ? vistaConectado(estado) : vistaDesconectado();
    document.getElementById("btn-calendar").onclick = conectarCalendar;
  } catch (e) {
    console.error("No se pudo obtener el estado del calendario", e);
    contenedor.innerHTML = vistaError();
    document.getElementById("btn-reintentar").onclick = cargarEstadoCalendar;
  }
}

async function iniciar() {
  const usuario = await Alfred.requerirSesion();
  if (!usuario) return; // requerirSesion ya redirigió al login
  pintarUsuario(usuario);
  await cargarEstadoCalendar();
}

iniciar();
