// Js/Home.js
// Dashboard de turnos del médico logueado.

const ESTADOS = {
  0: "Pendiente",
  1: "Confirmado",
  2: "Cancelado",
  3: "No asistió",
  4: "Completado",
};

function logout() {
  Alfred.cerrarSesion();
}

function pintarUsuario(usuario) {
  Alfred.setText("nombreUsuario", usuario.name || usuario.email);
  Alfred.setSrc("avatar", usuario.picture);
}

function formatearRango(inicioIso, finIso) {
  const inicio = new Date(inicioIso);
  const fin = new Date(finIso);
  const fecha = inicio.toLocaleDateString("es-AR", { day: "2-digit", month: "short", year: "numeric" });
  const horas = `${inicio.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" })}–${fin.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" })}`;
  return `${fecha} · ${horas}`;
}

// Construye el elemento de un turno con createElement (datos del usuario van por textContent).
function crearItemTurno(turno) {
  const item = document.createElement("div");
  item.className = "turno-item";
  item.style.cssText = "padding:12px 0; border-bottom:1px solid var(--border, #e5e7eb);";

  const cabecera = document.createElement("div");
  cabecera.style.cssText = "display:flex; justify-content:space-between; gap:8px;";

  const fecha = document.createElement("strong");
  fecha.textContent = formatearRango(turno.inicioUtc, turno.finUtc);

  const estado = document.createElement("span");
  estado.textContent = ESTADOS[turno.estado] ?? "Desconocido";
  estado.style.cssText = "color: var(--muted, #6b7280); font-size:0.9em;";

  cabecera.append(fecha, estado);
  item.append(cabecera);

  if (turno.motivo) {
    const motivo = document.createElement("div");
    motivo.textContent = turno.motivo;
    motivo.style.cssText = "color: var(--muted, #6b7280); font-size:0.9em; margin-top:4px;";
    item.append(motivo);
  }

  return item;
}

function pintarLista(contenedorId, turnos, mensajeVacio) {
  const contenedor = document.getElementById(contenedorId);
  if (!contenedor) return;
  contenedor.innerHTML = "";

  if (turnos.length === 0) {
    contenedor.textContent = mensajeVacio;
    return;
  }
  turnos.forEach((turno) => contenedor.append(crearItemTurno(turno)));
}

async function cargarTurnos() {
  let turnos;
  try {
    turnos = await Alfred.apiGet("/api/turnos");
  } catch (e) {
    console.error("No se pudieron cargar los turnos", e);
    Alfred.setText("turnos-proximos", "No se pudieron cargar los turnos.");
    Alfred.setText("turnos-historial", "");
    return;
  }

  const ahora = Date.now();
  const proximos = turnos
    .filter((t) => new Date(t.finUtc).getTime() >= ahora)
    .sort((a, b) => new Date(a.inicioUtc) - new Date(b.inicioUtc));
  const historial = turnos
    .filter((t) => new Date(t.finUtc).getTime() < ahora)
    .sort((a, b) => new Date(b.inicioUtc) - new Date(a.inicioUtc));

  pintarLista("turnos-proximos", proximos, "No tenés turnos próximos.");
  pintarLista("turnos-historial", historial, "Todavía no hay turnos en el historial.");
}

async function iniciar() {
  const usuario = await Alfred.requerirSesion();
  if (!usuario) return; // requerirSesion ya redirigió al login
  pintarUsuario(usuario);
  await cargarTurnos();
}

iniciar();
