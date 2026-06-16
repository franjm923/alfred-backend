// lib/utils.ts
export function cn(...classes: Array<string | undefined | null | false>) {
  return classes.filter(Boolean).join(" ");
}
// también default, por si algún archivo hace `import cn from "@/lib/utils"`
export default cn