#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

console.log('🔍 Verificando configuración de rutas...\n');

const checks = [];

// 1. Verificar index.html
const indexPath = path.join(__dirname, 'public', 'index.html');
if (fs.existsSync(indexPath)) {
  const content = fs.readFileSync(indexPath, 'utf8');
  
  // Verificar enlaces a /login
  const loginLinks = (content.match(/href="\/login"/g) || []).length;
  checks.push({
    name: 'Enlaces /login en index.html',
    pass: loginLinks >= 2,
    message: loginLinks >= 2 ? `✅ ${loginLinks} enlaces encontrados` : `❌ Solo ${loginLinks} enlaces encontrados`
  });
  
  // Verificar que no hay typos
  const hasTypo = content.includes('href="http/login"');
  checks.push({
    name: 'Sin typos en enlaces',
    pass: !hasTypo,
    message: hasTypo ? '❌ Typo encontrado: http/login' : '✅ No hay typos'
  });
  
  // Verificar WhatsApp actualizado
  const hasWhatsApp = content.includes('5491161616321');
  checks.push({
    name: 'WhatsApp actualizado',
    pass: hasWhatsApp,
    message: hasWhatsApp ? '✅ WhatsApp configurado' : '⚠️  WhatsApp con placeholder'
  });
} else {
  checks.push({
    name: 'index.html existe',
    pass: false,
    message: '❌ No se encuentra index.html'
  });
}

// 2. Verificar next.config.js
const configPath = path.join(__dirname, 'next.config.js');
if (fs.existsSync(configPath)) {
  const content = fs.readFileSync(configPath, 'utf8');
  const hasRewrite = content.includes('rewrites') && content.includes('/index.html');
  checks.push({
    name: 'Rewrite configurado en next.config.js',
    pass: hasRewrite,
    message: hasRewrite ? '✅ Rewrite "/" -> "/index.html"' : '❌ Rewrite no configurado'
  });
} else {
  checks.push({
    name: 'next.config.js existe',
    pass: false,
    message: '❌ No se encuentra next.config.js'
  });
}

// 3. Verificar .env.local
const envPath = path.join(__dirname, '.env.local');
if (fs.existsSync(envPath)) {
  const content = fs.readFileSync(envPath, 'utf8');
  const hasBackendUrl = content.includes('NEXT_PUBLIC_BACKEND_URL');
  checks.push({
    name: 'Variable NEXT_PUBLIC_BACKEND_URL',
    pass: hasBackendUrl,
    message: hasBackendUrl ? '✅ Configurada' : '❌ No configurada'
  });
} else {
  checks.push({
    name: '.env.local existe',
    pass: false,
    message: '⚠️  No se encuentra (opcional en local)'
  });
}

// 4. Verificar middleware.ts
const middlewarePath = path.join(__dirname, 'middleware.ts');
if (fs.existsSync(middlewarePath)) {
  const content = fs.readFileSync(middlewarePath, 'utf8');
  const hasConfig = content.includes('matcher') && content.includes('config');
  checks.push({
    name: 'Middleware configurado',
    pass: hasConfig,
    message: hasConfig ? '✅ Matcher configurado' : '⚠️  Sin matcher'
  });
} else {
  checks.push({
    name: 'middleware.ts existe',
    pass: true,
    message: '✅ Encontrado'
  });
}

// 5. Verificar vercel.json
const vercelPath = path.join(__dirname, 'vercel.json');
checks.push({
  name: 'vercel.json para deployment',
  pass: fs.existsSync(vercelPath),
  message: fs.existsSync(vercelPath) ? '✅ Configurado' : '⚠️  Opcional pero recomendado'
});

// 6. Verificar login-form.tsx
const loginFormPath = path.join(__dirname, 'components', 'login-form.tsx');
if (fs.existsSync(loginFormPath)) {
  const content = fs.readFileSync(loginFormPath, 'utf8');
  const hasOAuth = content.includes('NEXT_PUBLIC_BACKEND_URL') && content.includes('/login/google');
  checks.push({
    name: 'LoginForm conectado con OAuth',
    pass: hasOAuth,
    message: hasOAuth ? '✅ Backend OAuth configurado' : '❌ OAuth no configurado'
  });
}

// Mostrar resultados
console.log('📋 Resultados:\n');
let allPassed = true;
checks.forEach(check => {
  console.log(`${check.message}`);
  if (!check.pass && !check.message.includes('⚠️')) {
    allPassed = false;
  }
});

console.log('\n' + '='.repeat(50));
if (allPassed) {
  console.log('✅ Todo listo para deployment!');
  console.log('\n📝 Próximos pasos:');
  console.log('1. Ejecuta: npm run build');
  console.log('2. Verifica: npm run dev (localhost:3000)');
  console.log('3. Deploy en Vercel con las variables de entorno');
} else {
  console.log('⚠️  Hay algunos issues que revisar antes del deploy');
  console.log('Revisa DEPLOY.md para más información');
}
console.log('='.repeat(50) + '\n');
