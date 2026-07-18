"use client";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
export default function ModerationActions({ id }: { id: string }) { const router = useRouter(); async function resolve(action: "dismiss" | "delete") { try { await clientApi(`/api/admin/social/reports/${id}/resolve`, { method: "POST", body: { action } }); router.refresh(); } catch (error) { appToast.error(error, "Could not resolve report"); } } return <div className="flex gap-2"><button className="btn-ghost btn-sm" onClick={() => resolve("dismiss")}>Dismiss</button><button className="btn-danger btn-sm" onClick={() => resolve("delete")}>Remove content</button></div>; }
