"use client";
import { useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
export default function FollowRequestButton({ token }: { token: string }) { const [sent, setSent] = useState(false); async function request() { try { await clientApi("/api/Social/follows", { method: "POST", body: { shareToken: token } }); setSent(true); appToast.success("Follow request sent"); } catch (error) { appToast.error(error, "Sign in and create a social profile first"); } } return <button className="btn-primary" disabled={sent} onClick={request}>{sent ? "Request sent" : "Request to follow"}</button>; }
