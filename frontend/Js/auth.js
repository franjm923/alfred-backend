// Js/auth.js
// Helpers de autenticación reutilizables. La sesión vive en una cookie httpOnly
// que setea el backend tras el login con Google, por eso todo va con credentials.
const Alfred = window.Alfred || (window.Alfred = {});

// Redirige a Google. Al volver, el backend manda el navegador a Home.html.
Alfred.loginConGoogle = function () {
  const returnUrl = new URL("Home.html", window.location.href).href;
  window.location.href =
    `${Alfred.API_BASE}/login/google?returnUrl=${encodeURIComponent(returnUrl)}`;
};

// Escribe texto en un elemento por id sin romper si no existe.
// Útil mientras se rediseña el HTML: si falta un id, el resto sigue funcionando.
Alfred.setText = function (id, valor) {
  const el = document.getElementById(id);
  if (el) el.textContent = valor;
};

// Setea el src de una imagen por id sin romper si no existe.
Alfred.setSrc = function (id, url) {
  const el = document.getElementById(id);
  if (el && url) el.src = url;
};

// GET autenticado al backend. Devuelve el JSON o lanza si la respuesta falla.
Alfred.apiGet = async function (path) {
  const res = await fetch(`${Alfred.API_BASE}${path}`, { credentials: "include" });
  if (!res.ok) throw new Error(`GET ${path} → ${res.status}`);
  return res.json();
};

// POST autenticado al backend con cuerpo JSON.
Alfred.apiPost = async function (path, body) {
  const res = await fetch(`${Alfred.API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`POST ${path} → ${res.status}`);
  return res.json();
};

// Devuelve los datos del usuario logueado, o null si no hay sesión.
Alfred.obtenerUsuario = async function () {
  const res = await fetch(`${Alfred.API_BASE}/api/me`, { credentials: "include" });
  if (!res.ok) return null;
  return res.json();
};

Alfred.cerrarSesion = async function () {
  await fetch(`${Alfred.API_BASE}/logout`, { credentials: "include" });
  window.location.href = "login.html";
};

// Para páginas privadas: garantiza que haya sesión o redirige al login.
Alfred.requerirSesion = async function () {
  const usuario = await Alfred.obtenerUsuario();
  if (!usuario) {
    window.location.href = "login.html";
    return null;
  }
  return usuario;
};
