document.querySelector('.login-form').onsubmit = function(e) {
  e.preventDefault();

  const email = document.getElementById('email').value.trim();
  const password = document.getElementById('password').value;

  // Trae el usuario guardado del registro
  const savedEmail = localStorage.getItem('userEmail');
  const savedPassword = localStorage.getItem('userPassword');

  if (email === savedEmail && password === savedPassword) {
    // Redirige al home
    window.location.href = 'Home.html';
  } else {
    alert('Correo o contraseña incorrectos.');
  }
};
