// Js/Perfil.js
// Muestra los datos del médico logueado (solo lectura por ahora).

function logout() {
  Alfred.cerrarSesion();
}

function pintarUsuario(usuario) {
  Alfred.setText("nombreUsuario", usuario.name || usuario.email);
  Alfred.setSrc("avatar", usuario.picture);
}

function pintarPerfil(usuario) {
  const medico = usuario.medico;
  const texto = (valor) => valor || "—";

  Alfred.setText("campo-nombre", texto(medico?.nombreCompleto || usuario.name));
  Alfred.setText("campo-email", texto(usuario.email));
  Alfred.setText("campo-especialidad", texto(medico?.especialidad));
  Alfred.setText("campo-matricula", texto(medico?.matricula));
  Alfred.setText("campo-rol", texto(usuario.role));
}

async function iniciar() {
  const usuario = await Alfred.requerirSesion();
  if (!usuario) return; // requerirSesion ya redirigió al login
  pintarUsuario(usuario);
  pintarPerfil(usuario);
}

iniciar();
