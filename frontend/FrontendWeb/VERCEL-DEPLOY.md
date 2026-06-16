# 🚀 Guía Rápida de Deploy en Vercel

## Pre-requisitos
- [ ] Backend deployado (Render/Railway) y URL disponible
- [ ] Google OAuth configurado con redirect URIs correctos
- [ ] Repositorio en GitHub actualizado

## Pasos de Deploy

### 1. Preparar Variables de Entorno
Antes de hacer el deploy, tener lista la URL del backend:
```
NEXT_PUBLIC_BACKEND_URL=https://alfred-backend-xxxx.onrender.com
```

### 2. Deploy en Vercel

#### Opción A: Desde el Dashboard de Vercel
1. Ve a [vercel.com](https://vercel.com/dashboard)
2. Click en "Add New Project"
3. Selecciona tu repositorio de GitHub
4. Configura:
   - **Framework**: Next.js (auto-detectado)
   - **Root Directory**: `Frontend/FrontendWeb`
   - **Build Command**: `npm run build` (default)
   - **Output Directory**: `.next` (default)
5. En "Environment Variables" agrega:
   ```
   Name: NEXT_PUBLIC_BACKEND_URL
   Value: https://tu-backend.render.com
   ```
6. Click en "Deploy"

#### Opción B: Desde CLI
```bash
# Instalar Vercel CLI (solo primera vez)
npm i -g vercel

# En el directorio del proyecto
cd Frontend/FrontendWeb

# Login (solo primera vez)
vercel login

# Deploy
vercel

# Agregar variable de entorno
vercel env add NEXT_PUBLIC_BACKEND_URL
# Pegar: https://tu-backend.render.com
# Seleccionar: Production

# Deploy a producción
vercel --prod
```

### 3. Configurar Backend CORS

Una vez que tengas la URL de Vercel (ejemplo: `https://alfred-frontend.vercel.app`), actualiza el backend:

**En `Backend/Alfred2/Program.cs`:**
```csharp
var allowedOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ??
    "https://alfred-frontend.vercel.app,http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
```

**En las variables de entorno de Render:**
```
ALLOWED_ORIGINS=https://alfred-frontend.vercel.app,http://localhost:3000
```

### 4. Configurar Google OAuth

En [Google Cloud Console](https://console.cloud.google.com/apis/credentials):

**Authorized JavaScript origins:**
- `https://alfred-frontend.vercel.app`
- `http://localhost:3000`

**Authorized redirect URIs:**
- `https://alfred-frontend.vercel.app/login`
- `https://tu-backend.onrender.com/signin-google`
- `http://localhost:3000/login`
- `http://localhost:10000/signin-google`

### 5. Verificar Deploy

Una vez deployado:

1. **Landing page** - Visita `https://alfred-frontend.vercel.app`
   - ✅ Se debe ver el landing correctamente
   - ✅ CSS cargando bien
   - ✅ Botón "Ingresar" debe ir a `/login`

2. **Login page** - Click en "Ingresar" o visita `/login`
   - ✅ Formulario de login visible
   - ✅ Botón "Iniciar sesión con Google"

3. **Google OAuth** - Click en "Iniciar sesión con Google"
   - ✅ Redirecciona al backend
   - ✅ Muestra pantalla de Google
   - ✅ Después del login, redirecciona a `/home`

## Solución de Problemas

### Landing no se ve
```bash
# Verificar que index.html está en la build
vercel logs
# Buscar: "Static file copied: public/index.html"
```

### Login redirecciona a localhost
**Problema**: `NEXT_PUBLIC_BACKEND_URL` no está configurado
**Solución**:
```bash
vercel env add NEXT_PUBLIC_BACKEND_URL
# Valor: https://tu-backend.onrender.com

# Redeploy
vercel --prod
```

### CORS Error al hacer login
**Problema**: Backend no permite el dominio de Vercel
**Solución**: Actualizar `ALLOWED_ORIGINS` en el backend y redeploy

### Google OAuth falla
**Problema**: Redirect URIs no configurados
**Solución**: Agregar URLs en Google Cloud Console (ver paso 4)

## Comandos Útiles

```bash
# Ver logs en tiempo real
vercel logs --follow

# Ver información del proyecto
vercel project list

# Revertir a deploy anterior
vercel rollback

# Ver variables de entorno
vercel env ls

# Pull variables para local
vercel env pull
```

## Dominios Personalizados

Si quieres usar tu propio dominio:

1. En Vercel Dashboard → Settings → Domains
2. Agregar dominio (ej: `app.alfred.com`)
3. Configurar DNS según instrucciones de Vercel
4. Actualizar:
   - CORS en backend
   - Google OAuth redirect URIs
   - Variable `FRONTEND_REDIRECT_URL` en backend

## Checklist Final

- [ ] Frontend deployado en Vercel
- [ ] Variables de entorno configuradas
- [ ] Backend permite dominio de Vercel en CORS
- [ ] Google OAuth redirect URIs actualizados
- [ ] Landing page visible
- [ ] Login page funcional
- [ ] OAuth funciona end-to-end

## Links Útiles

- [Vercel Dashboard](https://vercel.com/dashboard)
- [Vercel Docs - Next.js](https://vercel.com/docs/frameworks/nextjs)
- [Google Cloud Console](https://console.cloud.google.com/)
- [Render Dashboard](https://dashboard.render.com/)

---

¿Problemas? Revisa `DEPLOY.md` para más detalles técnicos.
