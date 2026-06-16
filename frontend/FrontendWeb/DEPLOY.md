# Frontend Alfred - Guía de Deploy

## Estructura
- **Landing page**: `public/index.html` (estático)
- **App Next.js**: `/login`, `/home` (rutas dinámicas)
- **Backend**: `.NET API` con Google OAuth

## Variables de Entorno

### Desarrollo Local
```bash
NEXT_PUBLIC_BACKEND_URL=http://localhost:10000
```

### Producción (Vercel)
```bash
NEXT_PUBLIC_BACKEND_URL=https://tu-backend.render.com
```

## Deploy en Vercel

### 1. Conectar el repositorio
- Ve a [vercel.com](https://vercel.com)
- Click en "Add New Project"
- Importa tu repositorio de GitHub

### 2. Configurar el proyecto
- **Framework Preset**: Next.js
- **Root Directory**: `Frontend/FrontendWeb`
- **Build Command**: `npm run build`
- **Output Directory**: `.next`

### 3. Variables de entorno en Vercel
Agrega en el dashboard de Vercel:
```
NEXT_PUBLIC_BACKEND_URL=https://alfred-backend-xxxx.onrender.com
```

### 4. Deploy
- Click en "Deploy"
- Vercel automáticamente:
  - Instala dependencias
  - Ejecuta el build
  - Despliega la aplicación

## Routing

### Landing (/)
- Se sirve desde `public/index.html`
- Configurado en `next.config.js` con rewrite
- Enlaces a `/login` funcionan correctamente

### Login (/login)
- Ruta dinámica de Next.js
- Usa Google OAuth del backend
- Redirecciona a `/login/google` en el backend

### Home (/home)
- Requiere autenticación
- Dashboard del médico
- Muestra turnos y calendario

## Testing Local

```bash
# Instalar dependencias
npm install

# Ejecutar en desarrollo
npm run dev

# Build de producción
npm run build

# Servir build de producción
npm start
```

## Solución de Problemas

### Landing no se ve
- Verificar que `public/index.html` existe
- Verificar rewrite en `next.config.js`
- Clear cache de Vercel y redeploy

### Login no funciona
- Verificar `NEXT_PUBLIC_BACKEND_URL` en Vercel
- Verificar CORS en el backend
- Verificar que el backend esté corriendo

### Rutas 404
- Verificar `middleware.ts`
- Verificar matcher patterns
- Verificar que no haya conflictos en rewrites

## Backend CORS

El backend debe permitir tu dominio de Vercel:

```csharp
// En Program.cs
var allowedOrigins = new[] {
    "https://alfred-frontend.vercel.app",  // Tu dominio de Vercel
    "http://localhost:3000"                 // Local
};
```

## OAuth Redirect URIs

Configurar en Google Cloud Console:
- https://alfred-frontend.vercel.app/login
- https://tu-backend.render.com/signin-google
- http://localhost:3000/login (desarrollo)
- http://localhost:10000/signin-google (desarrollo)
