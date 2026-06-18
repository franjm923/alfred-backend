// Js/Perfil.js
// Perfil del médico: cargar especialidad/matrícula y enviar a verificación.

const ESTADOS_TEXTO = {
  Borrador: "Borrador (sin enviar)",
  Pendiente: "Pendiente de aprobación",
  Autorizado: "Autorizado ✅",
  Rechazado: "Rechazado",
};

function logout() {
  Alfred.cerrarSesion();
}

function pintarUsuario(usuario) {
  Alfred.setText("nombreUsuario", usuario.name || usuario.email);
  Alfred.setSrc("avatar", usuario.picture);
}

function bloquearFormulario() {
  document.getElementById("perfil-form")
    ?.querySelectorAll("input, button")
    .forEach((el) => (el.disabled = true));
}

function pintarPerfil(usuario) {
  const medico = usuario.medico;
  Alfred.setText("campo-nombre", medico?.nombreCompleto || usuario.name || "—");
  Alfred.setText("campo-email", usuario.email || "—");

  const estado = medico?.estadoVerificacion || "Borrador";
  Alfred.setText("campo-estado", ESTADOS_TEXTO[estado] || estado);

  const espInput = document.getElementById("input-especialidad");
  const matInput = document.getElementById("input-matricula");
  if (espInput) espInput.value = medico?.especialidad || "";
  if (matInput) matInput.value = medico?.matricula || "";

  // Una vez enviado (Pendiente) o aprobado, el form queda bloqueado.
  if (estado === "Pendiente" || estado === "Autorizado") {
    bloquearFormulario();
    if (estado === "Pendiente") {
      Alfred.setText("perfil-result", "Se ha enviado su información, espere a que sea aceptado.");
    }
  }
}

async function guardarPerfil(e) {
  e.preventDefault();
  const body = {
    especialidad: document.getElementById("input-especialidad").value.trim() || null,
    matricula: document.getElementById("input-matricula").value.trim() || null,
  };
  try {
    await Alfred.apiPut("/api/medico/perfil", body);
    Alfred.setText("perfil-result", "✅ Perfil guardado.");
  } catch (err) {
    console.error("No se pudo guardar el perfil", err);
    Alfred.setText("perfil-result", "⚠️ No se pudo guardar el perfil.");
  }
}

async function enviarAVerificacion() {
  try {
    await Alfred.apiPost("/api/medico/verificacion");
    Alfred.setText("perfil-result", "Se ha enviado su información, espere a que sea aceptado.");
    bloquearFormulario();
  } catch (err) {
    console.error("No se pudo enviar a verificación", err);
    Alfred.setText("perfil-result", "⚠️ Cargá especialidad y matrícula (y guardá) antes de enviar.");
  }
}

async function iniciar() {
  const usuario = await Alfred.requerirSesion();
  if (!usuario) return; // requerirSesion ya redirigió al login
  pintarUsuario(usuario);
  pintarPerfil(usuario);
  document.getElementById("perfil-form")?.addEventListener("submit", guardarPerfil);
  document.getElementById("btn-enviar-verificacion")?.addEventListener("click", enviarAVerificacion);
}

iniciar();
