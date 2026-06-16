/* ===== Smooth scroll para anclas internas ===== */
document.addEventListener('click', (e) => {
  const a = e.target.closest('a[href^="#"]');
  if (!a) return;

  const target = document.querySelector(a.getAttribute('href'));
  if (!target) return;

  e.preventDefault();
  target.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

/* ===== Reveal on scroll (features y secciones) ===== */
const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

if (!prefersReducedMotion && 'IntersectionObserver' in window) {
  const toReveal = document.querySelectorAll('.feature');
  const io = new IntersectionObserver((entries, obs) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('reveal-in');
        obs.unobserve(entry.target);
      }
    });
  }, { threshold: 0.12 });

  toReveal.forEach(el => io.observe(el));
} else {
  // Si prefiere menos movimiento, mostrar todo sin animar
  document.querySelectorAll('.feature').forEach(el => el.classList.add('reveal-in'));
}

/* ===== Mejora de accesibilidad: “skip focus” al hacer click con mouse ===== */
(function improveFocus(){
  function handleMouseDown(){ document.body.classList.add('user-using-mouse'); }
  function handleKeyDown(e){
    if (e.key === 'Tab') document.body.classList.remove('user-using-mouse');
  }
  window.addEventListener('mousedown', handleMouseDown);
  window.addEventListener('keydown', handleKeyDown);
})();

/* ===== Botoncito: onda/ripple simple opcional ===== */
(function ripple(){
  const buttons = document.querySelectorAll('.btn');
  buttons.forEach(btn => {
    btn.style.overflow = 'hidden';
    btn.addEventListener('click', (e) => {
      const r = document.createElement('span');
      const rect = btn.getBoundingClientRect();
      const size = Math.max(rect.width, rect.height);
      r.style.position = 'absolute';
      r.style.width = r.style.height = size + 'px';
      r.style.left = (e.clientX - rect.left - size/2) + 'px';
      r.style.top = (e.clientY - rect.top - size/2) + 'px';
      r.style.borderRadius = '50%';
      r.style.background = 'rgba(255,255,255,.25)';
      r.style.transform = 'scale(0)';
      r.style.transition = 'transform .45s ease, opacity .6s ease';
      r.style.pointerEvents = 'none';
      btn.appendChild(r);
      requestAnimationFrame(() => { r.style.transform = 'scale(1)'; r.style.opacity = '0'; });
      setTimeout(() => r.remove(), 600);
    });
  });
})();

/* ===== Lazy-load de imágenes si no usás loading="lazy" ===== */
(function lazyImages(){
  if ('loading' in HTMLImageElement.prototype) return; // ya soporta loading
  const imgs = document.querySelectorAll('img[loading="lazy"]');
  if (!imgs.length) return;
  const io = new IntersectionObserver((entries, obs) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        const img = entry.target;
        img.src = img.dataset.src || img.src;
        obs.unobserve(img);
      }
    });
  }, { rootMargin: '300px' });
  imgs.forEach(img => io.observe(img));
})();