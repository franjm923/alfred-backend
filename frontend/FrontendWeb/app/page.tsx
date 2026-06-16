"use client";
// app/page.tsx
import { useEffect } from "react";

// Nota: idealmente este archivo no debería existir para que "/" use el rewrite a /index.html.
// Si por alguna razón Next mantiene el route "/", hacemos un redirect del lado del cliente
// hacia el landing estático en /index.html.
export default function RootPage() {
  useEffect(() => {
    if (typeof window !== "undefined") {
      window.location.replace("/index.html");
    }
  }, []);
  return null;
}
