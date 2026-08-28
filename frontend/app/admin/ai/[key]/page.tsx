import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError } from "@/lib/api";
import { getAiPrompt, getAiEvalMatrix, type AiEvalMatrix } from "@/data/admin/ai";
import PromptConsole from "@/components/admin/ai/PromptConsole";

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
		<div className="space-y-6">
			<header className="space-y-2">
				<Link
					href="/admin/ai"
					className="text-xs text-charcoal-blue-500 hover:text-verdigris-600 dark:text-charcoal-blue-400"
				>
					← Assistant
				</Link>
				<h1 className="font-mono text-2xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{prompt.key}
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{prompt.description}
				</p>
			</header>

			<PromptConsole prompt={prompt} initialMatrix={matrix} />
		</div>
	);
}
