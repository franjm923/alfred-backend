# ============================
# 1) Etapa de build (compila)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar la solución y restaurar dependencias
COPY *.sln ./
COPY Alfred2/*.csproj ./Alfred2/
RUN dotnet restore

# Copiar el resto del código y publicar en Release
COPY . .
WORKDIR /src/Alfred2
RUN dotnet publish -c Release -o /app

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