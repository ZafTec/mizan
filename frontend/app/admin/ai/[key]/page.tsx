import { notFound } from "next/navigation";
import { ApiError } from "@/lib/api";
import {
  getAiPrompt,
  getAiEvalMatrix,
  type AiEvalMatrix,
} from "@/data/admin/ai";
import PromptConsole from "@/components/admin/ai/PromptConsole";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export default async function AdminAiPromptPage({
  params,
}: {
  params: Promise<{ key: string }>;
}) {
  const { key } = await params;
  const decoded = decodeURIComponent(key);

  let prompt;
  try {
    prompt = await getAiPrompt(decoded);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  // The matrix for whichever version the console opens on, so the eval tab is
  // populated on first paint rather than after a round trip.
  const initial = prompt.versions[0];
  let matrix: AiEvalMatrix | null = null;
  if (initial) {
    matrix = await getAiEvalMatrix(initial.id);
  }

  return (
    <>
      <AdminPageHeader
        title={prompt.key}
        lead={prompt.description}
        back={{ href: "/admin/ai", label: "Assistant" }}
      />

      <PromptConsole prompt={prompt} initialMatrix={matrix} />
    </>
  );
}
