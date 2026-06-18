# ============================
# 1) Etapa de build (compila)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo el proyecto de la app y restaurar
# (NO la solución: incluye Alfred2.Tests, que no va a la imagen de prod)
COPY Alfred2/*.csproj ./Alfred2/
RUN dotnet restore Alfred2/Alfred2.csproj

# Copiar el resto del código y publicar solo el proyecto de la app
COPY Alfred2/ ./Alfred2/
WORKDIR /src/Alfred2
RUN dotnet publish Alfred2.csproj -c Release -o /app

# ============================
# 2) Etapa final (runtime)
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Render expone el puerto 5000
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Alfred2.dll"]
