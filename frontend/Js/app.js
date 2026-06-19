/* =========================================================================
   Alfred — App shell compartido (sidebar + topbar + interacciones)
   Cada página define <aside id="sidebar" data-active="..."> y
   <div id="topActions"> y este script los completa.
   ========================================================================= */
(function () {
  "use strict";

  /* ---------- Datos del médico (demo) ---------- */
  var DOCTOR = { name: "Dra. Lucía Bravo", role: "Clínica médica", initial: "L" };

  /* ---------- Íconos ---------- */
  var I = {
    home:    '<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V21h14V9.5"/><path d="M9 21v-6h6v6"/>',
    agenda:  '<rect x="3" y="4.5" width="18" height="17" rx="2.5"/><path d="M16 2.5v4M8 2.5v4M3 9.5h18"/>',
    heart:   '<path d="M20.8 5.6a5.5 5.5 0 0 0-7.8 0L12 6.6l-1-1a5.5 5.5 0 1 0-7.8 7.8l1 1L12 22l7.8-7.6 1-1a5.5 5.5 0 0 0 0-7.8z"/>',
    dollar:  '<path d="M12 1.5v21"/><path d="M17 5.5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>',
    chart:   '<path d="M3 3v18h18"/><rect x="7" y="11" width="3" height="6" rx="1"/><rect x="13" y="7" width="3" height="10" rx="1"/>',
    cap:     '<path d="M22 10 12 5 2 10l10 5 10-5z"/><path d="M6 12v5c0 1 2.7 2.5 6 2.5s6-1.5 6-2.5v-5"/>',
    users:   '<path d="M17 20v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="3.5"/><path d="M23 20v-2a4 4 0 0 0-3-3.85"/><path d="M16 3.6a4 4 0 0 1 0 7.75"/>',
    gift:    '<rect x="3" y="8" width="18" height="13" rx="2"/><path d="M3 12h18"/><path d="M12 8V5.5a2.5 2.5 0 0 0-2.5-2.5C8 3 7 4.5 7 5.5S8 8 12 8zm0 0V5.5a2.5 2.5 0 0 1 2.5-2.5C16 3 17 4.5 17 5.5S16 8 12 8z"/>',
    book:    '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>',
    lock:    '<rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    logout:  '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5"/><path d="M21 12H9"/>',
    search:  '<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',
    bell:    '<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9z"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>',
    user:    '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
    gear:    '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
    chev:    '<path d="m6 9 6 6 6-6"/>',
    menu:    '<path d="M3 6h18M3 12h18M3 18h18"/>'
  };

  var NAV = [
    { id: "home",         label: "Home",         href: "home.html",         icon: "home" },
    { id: "agenda",       label: "Agenda",       href: "agenda.html",       icon: "agenda" },
    { id: "consultantes", label: "Consultantes", href: "consultantes.html", icon: "heart" },
    { id: "ingresos",     label: "Ingresos",     href: "ingresos.html",     icon: "dollar" },
    { id: "metricas",     label: "Métricas",     href: "metricas.html",     icon: "chart" },
    { id: "recursos",     label: "Recursos",     href: "recursos.html",     icon: "cap" },
    { id: "comunidad",    label: "Comunidad",    href: "#",                 icon: "users", disabled: true },
    { id: "beneficios",   label: "Beneficios",   href: "beneficios.html",   icon: "gift" },
    { id: "guias",        label: "Guías",        href: "guias.html",        icon: "book" }
  ];

  function svg(paths, sw) {
    return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="' + (sw || 2) +
      '" stroke-linecap="round" stroke-linejoin="round">' + paths + '</svg>';
  }

  /* ---------- Render sidebar ---------- */
  function renderSidebar(el) {
    var active = el.getAttribute("data-active") || "home";
    var links = NAV.map(function (n) {
      var cls = "side-link" + (n.id === active ? " active" : "") + (n.disabled ? " is-disabled" : "");
      var aria = n.id === active ? ' aria-current="page"' : (n.disabled ? ' aria-disabled="true" tabindex="-1"' : "");
      var lock = n.disabled ? '<span class="lock">' + svg(I.lock) + "</span>" : "";
      return '<a href="' + n.href + '" class="' + cls + '"' + aria + ">" + svg(n.icon === "agenda" || n.icon === "gift" || n.icon === "book" ? I[n.icon] : I[n.icon]) + n.label + lock + "</a>";
    }).join("");

    el.innerHTML =
      '<a href="index.html" class="brand">' +
        '<span class="brand-mark" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"/></svg></span>' +
        '<span class="brand-name">Alfred</span>' +
      "</a>" +
      '<nav class="side-nav" aria-label="Navegación de la app">' + links + "</nav>" +
      '<div class="side-foot"><div class="side-user">' +
        '<span class="avatar">' + DOCTOR.initial + "</span>" +
        '<div class="meta"><strong>' + DOCTOR.name + "</strong><span>" + DOCTOR.role + "</span></div>" +
        '<a href="login.html" class="out" aria-label="Cerrar sesión">' + svg(I.logout) + "</a>" +
      "</div></div>";
  }

  /* ---------- Render acciones de la topbar ---------- */
  function renderTopActions(el) {
    el.innerHTML =
      '<div class="search">' + svg(I.search) +
        '<input type="text" placeholder="Buscar paciente o turno…" aria-label="Buscar" /></div>' +
      '<button class="icon-btn" aria-label="Notificaciones"><span class="badge-dot"></span>' + svg(I.bell) + "</button>" +
      '<div class="profile-wrap">' +
        '<button class="profile-btn" id="profileBtn" aria-haspopup="true" aria-expanded="false">' +
          '<span class="avatar">' + DOCTOR.initial + "</span>" +
          '<span class="pname">' + DOCTOR.name + "</span>" +
          '<span class="chev">' + svg(I.chev) + "</span>" +
        "</button>" +
        '<div class="profile-menu" id="profileMenu" role="menu">' +
          '<div class="pm-head"><span class="avatar">' + DOCTOR.initial + "</span>" +
            '<div><strong>' + DOCTOR.name + "</strong><span>" + DOCTOR.role + "</span></div></div>" +
          '<a href="profile.html" role="menuitem">' + svg(I.user) + "Mi perfil</a>" +
          '<a href="#" role="menuitem">' + svg(I.gear) + "Configuración</a>" +
          '<div class="pm-sep"></div>' +
          '<a href="login.html" role="menuitem" class="danger">' + svg(I.logout) + "Cerrar sesión</a>" +
        "</div>" +
      "</div>";
  }

  /* ---------- Boot ---------- */
  document.addEventListener("DOMContentLoaded", function () {
    var sidebar = document.getElementById("sidebar");
    if (sidebar) renderSidebar(sidebar);
    var top = document.getElementById("topActions");
    if (top) renderTopActions(top);

    // Menú lateral en mobile
    var shell = document.getElementById("appShell");
    var menuBtn = document.getElementById("appMenuBtn");
    if (menuBtn && shell) {
      menuBtn.addEventListener("click", function () { shell.classList.toggle("nav-open"); });
      shell.addEventListener("click", function (e) {
        if (e.target === shell && shell.classList.contains("nav-open")) shell.classList.remove("nav-open");
      });
    }

    // Dropdown de perfil
    var pBtn = document.getElementById("profileBtn");
    var pMenu = document.getElementById("profileMenu");
    if (pBtn && pMenu) {
      pBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        var open = pMenu.classList.toggle("open");
        pBtn.setAttribute("aria-expanded", open ? "true" : "false");
      });
      document.addEventListener("click", function (e) {
        if (!pMenu.contains(e.target) && !pBtn.contains(e.target)) {
          pMenu.classList.remove("open");
          pBtn.setAttribute("aria-expanded", "false");
        }
      });
      document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") { pMenu.classList.remove("open"); pBtn.setAttribute("aria-expanded", "false"); }
      });
    }
  });
})();
