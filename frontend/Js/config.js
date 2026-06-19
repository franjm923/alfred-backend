// Js/config.js
// Única fuente de verdad para la URL del backend.
// En localhost usa el backend local; en cualquier otro host, el de producción.
var Alfred = window.Alfred || (window.Alfred = {});

const corriendoEnLocal = ["localhost", "127.0.0.1"].includes(window.location.hostname);

Alfred.API_BASE = corriendoEnLocal
  ? "http://localhost:5000"
  : "https://alfred-backend-3ohw.onrender.com";
