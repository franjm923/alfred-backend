window.onload = () => {
  document.getElementById('nombre').value = localStorage.getItem('userNombre') || 'Francisco';
  document.getElementById('apellido').value = localStorage.getItem('userApellido') || 'Juarez';
  document.getElementById('dni').value = localStorage.getItem('userDNI') || '';
  document.getElementById('telefono').value = localStorage.getItem('userTelefono') || '';
};

document.getElementById('perfil-form').onsubmit = function (e) {
  e.preventDefault();

  const dni = document.getElementById('dni').value.trim();
  const telefono = document.getElementById('telefono').value.trim();

  if (!dni.match(/^\d{7,9}$/)) {
    alert('Por favor, ingresá un DNI válido (7 a 9 números sin puntos ni guiones).');
    return;
  }

  if (telefono.length < 6) {
    alert('Por favor, ingresá un número de teléfono válido.');
    return;
  }

  localStorage.setItem('userDNI', dni);
  localStorage.setItem('userTelefono', telefono);

  alert('Datos guardados correctamente.');
};