// FrontendWeb/middleware.ts
import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

// Middleware simple
export function middleware(req: NextRequest) {
  return NextResponse.next();
}

// Matcher para rutas protegidas
export const config = {
  matcher: [
    "/((?!api|_next/static|_next/image|favicon.ico|assets|css).*)",
  ],
};
