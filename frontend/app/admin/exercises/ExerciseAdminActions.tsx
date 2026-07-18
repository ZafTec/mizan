"use client";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
export default function ExerciseAdminActions({ id, custom }: { id: string; custom: boolean }) { const router = useRouter(); async function run(path: string, method: "POST" | "DELETE") { try { await clientApi(path, { method }); router.refresh(); } catch (error) { appToast.error(error, "Could not update exercise"); } } return <div className="flex justify-end gap-2">{custom && <button className="btn-primary btn-sm" onClick={() => run(`/api/Exercises/${id}/promote`, "POST")}>Promote</button>}<button className="btn-ghost btn-sm text-red-600" onClick={() => run(`/api/Exercises/${id}`, "DELETE")}>Delete</button></div>; }
