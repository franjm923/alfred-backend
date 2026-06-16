import { NextResponse } from "next/server";
const BACKEND_URL = process.env.BACKEND_URL;

export async function GET() {
  return NextResponse.json({ ok: true, msg: "session route ok" });
}

export async function POST(req: Request) {
  // soporta <form> y JSON
  let email = "", password = "";
  const ct = req.headers.get("content-type") || "";
  if (ct.includes("application/json")) {
    const body = await req.json().catch(()=>({}));
    email = body?.email ?? "";
    password = body?.password ?? "";
  } else {
    const form = await req.formData();
    email = String(form.get("email") ?? "");
    password = String(form.get("password") ?? "");
  }

  if (!BACKEND_URL) {
    return NextResponse.redirect(new URL("/login?e=cfg", req.url), { status: 302 });
  }

  let data: any = {};
  let ok = false;
  try {
    const res = await fetch(`${BACKEND_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
      cache: "no-store",
    });
    ok = res.ok;
    data = await res.json().catch(()=> ({}));
  } catch (e) {
    ok = false;
  }

  if (!ok || !data?.token) {
    return NextResponse.redirect(new URL("/login?e=1", req.url), { status: 302 });
  }

  const resp = NextResponse.redirect(new URL("/", req.url));
  resp.cookies.set("alfred_token", data.token, {
    httpOnly: true, secure: process.env.NODE_ENV === "production",
    sameSite: "lax", path: "/", maxAge: 60 * 60 * 24 * 7,
  });
  return resp;
}

export async function DELETE() {
  const resp = NextResponse.json({ ok: true });
  resp.cookies.set("alfred_token", "", { path: "/", maxAge: 0 });
  return resp;
}
