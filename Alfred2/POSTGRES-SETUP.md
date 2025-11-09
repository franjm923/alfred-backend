# 🔧 Solución: PostgreSQL no está corriendo

## El Error

```
Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:5432
No se puede establecer una conexión ya que el equipo de destino denegó expresamente dicha conexión.
```

**Causa**: El backend intenta conectarse a PostgreSQL en localhost:5432, pero no está instalado o no está corriendo.

## Solución Rápida: Instalar PostgreSQL

### Opción 1: PostgreSQL con Docker (Recomendado)

Si tienes Docker Desktop instalado:

```bash
# Iniciar PostgreSQL en Docker
docker run --name alfred-postgres `
  -e POSTGRES_PASSWORD=alfred123 `
  -e POSTGRES_DB=alfred `
  -p 5432:5432 `
  -d postgres:15

# Verificar que está corriendo
docker ps
```

Luego actualiza `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=alfred;Username=postgres;Password=alfred123"
  }
}
```

### Opción 2: PostgreSQL Nativo (Windows)

1. **Descargar**: https://www.postgresql.org/download/windows/
2. **Instalar**: Usa el instalador (recuerda la contraseña que pongas)
3. **Verificar**: 
   ```bash
   # En PowerShell
   psql -U postgres -c "SELECT version();"
   ```

4. **Crear base de datos**:
   ```bash
   psql -U postgres
   # Dentro de psql:
   CREATE DATABASE alfred;
   \q
   ```

5. **Actualizar appsettings.json**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=alfred;Username=postgres;Password=TU_PASSWORD_AQUI"
     }
   }
   ```

### Opción 3: SQLite (Temporal, solo para pruebas)

Si solo quieres probar rápidamente sin instalar PostgreSQL:

1. **Instalar paquete SQLite**:
   ```bash
   cd f:\Alfred\Backend\Alfred2
   dotnet add package Microsoft.EntityFrameworkCore.Sqlite
   ```

2. **Crear appsettings.Development.json**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=alfred.db"
     },
     "UseSqlite": true
   }
   ```

3. **Modificar Program.cs** (temporal):
   ```csharp
   // En lugar de:
   // opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
   
   // Usar:
   var useSqlite = builder.Configuration.GetValue<bool>("UseSqlite");
   if (useSqlite)
       opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
   else
       opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
   ```

**⚠️ IMPORTANTE**: SQLite es solo para desarrollo local. Render usa PostgreSQL.

## Después de Instalar PostgreSQL

1. **Ejecutar el backend**:
   ```bash
   cd f:\Alfred\Backend\Alfred2
   dotnet run
   ```

2. **Las migraciones se ejecutan automáticamente** al iniciar

3. **Verificar**:
   - Health check: http://localhost:10000/healthz
   - Respuesta: `{"status":"ok"}`

## Verificar que PostgreSQL está Corriendo

### Docker:
```bash
docker ps
# Debe aparecer alfred-postgres en la lista
```

### Windows Service:
```bash
# PowerShell como Admin
Get-Service -Name postgresql*
# Debe estar "Running"
```

### Conexión directa:
```bash
psql -U postgres -d alfred
# Si conecta, PostgreSQL está OK
```

## Comandos Útiles PostgreSQL

```bash
# Ver bases de datos
psql -U postgres -c "\l"

# Conectar a alfred
psql -U postgres -d alfred

# Ver tablas (dentro de psql)
\dt

# Ver contenido de tabla
SELECT * FROM "Users";

# Salir
\q
```

## Alternativa: Usar Base de Datos en la Nube (Gratis)

Si no quieres instalar PostgreSQL localmente, puedes usar una base de datos en la nube:

### Supabase (Gratis)
1. Crea cuenta en https://supabase.com
2. Crea un proyecto
3. Ve a Settings → Database
4. Copia la "Connection string"
5. Pégala en `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "postgresql://postgres:[PASSWORD]@db.xxx.supabase.co:5432/postgres"
     }
   }
   ```

### ElephantSQL (Gratis)
1. Crea cuenta en https://www.elephantsql.com
2. Crea una instancia "Tiny Turtle" (gratis)
3. Copia la URL
4. Úsala en `appsettings.json`

## Resumen

**Opción más rápida**: Docker
```bash
docker run --name alfred-postgres -e POSTGRES_PASSWORD=alfred123 -e POSTGRES_DB=alfred -p 5432:5432 -d postgres:15
```

**Opción más permanente**: Instalar PostgreSQL nativo

**Opción para probar**: Base de datos en la nube (Supabase/ElephantSQL)

Una vez que PostgreSQL esté corriendo, el backend iniciará sin problemas.
