// FrontendWeb/app/api/oauth/google/callback/route.ts
import { NextResponse } from "next/server";

export async function GET(req: Request) {
  const url = new URL(req.url);
  const token = url.searchParams.get("token");

  if (!token) {
    // si tu backend vuelve con ?code=..., acá deberías intercambiarlo por token
    return NextResponse.redirect(new URL("/login?e=1", req.url));
  }

  const resp = NextResponse.redirect(new URL("/", req.url));
  resp.cookies.set("alfred_token", token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 7,
  });
  return resp;
}
