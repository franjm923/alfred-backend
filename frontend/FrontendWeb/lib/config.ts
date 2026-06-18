// lib/config.ts
// Única fuente de verdad para la URL del backend (puerto 5000, ver Dockerfile).
export const BACKEND_URL =
  process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:5000";
