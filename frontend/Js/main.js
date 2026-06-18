/* =========================================================================
   Alfred — interacciones del sitio
   Vanilla JS, sin dependencias.
   ========================================================================= */
(function () {
  "use strict";

  /* ---------- Header: sombra al hacer scroll ---------- */
  var header = document.getElementById("siteHeader");
  if (header) {
    var onScroll = function () {
      header.classList.toggle("scrolled", window.scrollY > 8);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
  }

  /* ---------- Menú móvil ---------- */
  var navToggle = document.getElementById("navToggle");
  if (navToggle && header) {
    navToggle.addEventListener("click", function () {
      var open = header.classList.toggle("open");
      navToggle.setAttribute("aria-expanded", open ? "true" : "false");
    });
    // Cerrar al tocar un enlace
    header.querySelectorAll(".nav-links a").forEach(function (a) {
      a.addEventListener("click", function () {
        header.classList.remove("open");
        navToggle.setAttribute("aria-expanded", "false");
      });
    });
  }

  /* ---------- Reveal on scroll ---------- */
  var revealEls = document.querySelectorAll(".reveal");
  if (revealEls.length) {
    if ("IntersectionObserver" in window) {
      var io = new IntersectionObserver(
        function (entries) {
          entries.forEach(function (entry) {
            if (entry.isIntersecting) {
              entry.target.classList.add("in");
              io.unobserve(entry.target);
            }
          });
        },
        { threshold: 0.14, rootMargin: "0px 0px -8% 0px" }
      );
      revealEls.forEach(function (el, i) {
        el.style.transitionDelay = (i % 4) * 70 + "ms";
        io.observe(el);
      });
    } else {
      revealEls.forEach(function (el) { el.classList.add("in"); });
    }
  }

  /* ---------- Login: mostrar/ocultar contraseña ---------- */
  var togglePass = document.getElementById("togglePass");
  var passInput = document.getElementById("password");
  if (togglePass && passInput) {
    togglePass.addEventListener("click", function () {
      var isPass = passInput.type === "password";
      passInput.type = isPass ? "text" : "password";
      togglePass.setAttribute("aria-label", isPass ? "Ocultar contraseña" : "Mostrar contraseña");
    });
  }

  /* ---------- Login: botón de Google (demo) ---------- */
  var googleBtn = document.getElementById("googleBtn");
  if (googleBtn) {
    googleBtn.addEventListener("click", function () {
      // Punto de integración: acá iría el flujo OAuth de Google.
      googleBtn.disabled = true;
      googleBtn.style.opacity = "0.7";
      googleBtn.textContent = "Conectando con Google…";
    });
  }

  /* ---------- Login: validación del formulario ---------- */
  var form = document.getElementById("loginForm");
  if (form) {
    var email = document.getElementById("email");
    var pass = document.getElementById("password");
    var emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    var clearOnInput = function (input) {
      input.addEventListener("input", function () {
        input.classList.remove("invalid");
      });
    };
    if (email) clearOnInput(email);
    if (pass) clearOnInput(pass);

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var ok = true;

      if (!email.value || !emailRe.test(email.value.trim())) {
        email.classList.add("invalid");
        ok = false;
      }
      if (!pass.value || pass.value.length < 6) {
        pass.classList.add("invalid");
        ok = false;
      }
      if (!ok) {
        var firstBad = form.querySelector(".invalid");
        if (firstBad) firstBad.focus();
        return;
      }

      // Punto de integración: acá iría el envío real al backend.
      var submitBtn = form.querySelector('button[type="submit"]');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = "Ingresando…";
      }
    });
  }
})();
