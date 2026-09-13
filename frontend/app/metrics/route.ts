import { registry } from "@/lib/metrics";

export const dynamic = "force-dynamic";

export async function GET() {
  const body = await registry.metrics();
  return new Response(body, {
    headers: { "Content-Type": registry.contentType },
  });
}
