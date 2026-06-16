document.querySelector('.registro-form').onsubmit = function(e) {
  e.preventDefault();

  const nombre = document.getElementById('nombre').value.trim();
  const apellido = document.getElementById('apellido').value.trim();
  const dni = document.getElementById('dni').value.trim();
  const email = document.getElementById('email').value.trim();
  const telefono = document.getElementById('telefono').value.trim();
  const password = document.getElementById('password').value;
  const confirmar = document.getElementById('confirmar').value;
  const terminos = document.getElementById('terminos').checked;

  if (nombre.length < 2 && nombre == Number(nombre)) {
    alert('Por favor, ingresá un nombre válido.');
    return;
  }
  if (apellido.length < 2 && apellido == Number(apellido)) {
    alert('Por favor, ingresá un apellido válido.');
    return;
  }
  if (!/^\d{7,9}$/.test(dni)) {
    alert('El DNI debe ser solo números (7 u 8 dígitos, sin puntos ni guiones).');
    return;
  }
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    alert('Por favor, ingresá un correo electrónico válido.');
    return;
  }
  if (telefono.length < 6) {
    alert('Por favor, ingresá un número de teléfono válido.');
    return;
  }
  if (password.length < 6) {
    alert('La contraseña debe tener al menos 6 caracteres.');
    return;
  }
  if (password !== confirmar) {
    alert('Las contraseñas no coinciden.');
    return;
  }
  if (!terminos) {
    alert('Debés aceptar los Términos y condiciones.');
    return;
  }

  localStorage.setItem('userEmail', email);
  localStorage.setItem('userPassword', password);

  alert('¡Cuenta creada con éxito!');
  window.location.href = 'index.html';
};