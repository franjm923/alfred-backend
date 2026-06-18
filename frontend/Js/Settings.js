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

// Panel de admin: solo visible para rol "admin". Registra un médico de prueba.
function configurarPanelAdmin(usuario) {
  if (usuario.role !== "admin") return;

  const card = document.getElementById("admin-card");
  if (card) card.style.display = "";

  const form = document.getElementById("admin-medico-form");
  if (!form) return;

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const resultado = document.getElementById("admin-medico-result");
    const idRaw = document.getElementById("admin-medico-id").value.trim();
    const body = {
      medicoId: idRaw || null,
      numero: document.getElementById("admin-medico-numero").value.trim(),
      nombreCompleto: document.getElementById("admin-medico-nombre").value.trim() || null,
    };
    try {
      const medico = await Alfred.apiPost("/api/admin/medicos", body);
      resultado.textContent =
        `✅ Médico de prueba listo: ${medico.nombreCompleto} (Id ${medico.id}, número ${medico.telefonoE164})`;
    } catch (err) {
      console.error("No se pudo guardar el médico de prueba", err);
      resultado.textContent = "⚠️ No se pudo guardar el médico de prueba.";
    }
  });
}

async function iniciar() {
  const usuario = await Alfred.requerirSesion();
  if (!usuario) return; // requerirSesion ya redirigió al login
  pintarUsuario(usuario);
  configurarPanelAdmin(usuario);
  await cargarEstadoCalendar();
}

iniciar();
