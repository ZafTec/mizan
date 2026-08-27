"use client";

import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { cn } from "@/lib/utils";

/**
 * Assistant replies arrive as markdown, so rendering them raw showed people
 * literal asterisks and pipe-delimited tables.
 *
 * No `rehype-raw`: the model's output is untrusted text, and enabling raw HTML
 * here would turn a prompt injection into script execution. react-markdown
 * escapes HTML by default and that default is the point.
 */
export default function AiMarkdown({
	content,
	onDark = false,
}: {
	content: string;
	onDark?: boolean;
}) {
	return (
		<div
			className={cn(
				"space-y-3 text-sm leading-relaxed",
				"[&_p]:m-0",
				"[&_ul]:m-0 [&_ul]:list-disc [&_ul]:space-y-1 [&_ul]:pl-5",
				"[&_ol]:m-0 [&_ol]:list-decimal [&_ol]:space-y-1 [&_ol]:pl-5",
				"[&_li]:marker:text-current/60",
				"[&_h1]:text-base [&_h1]:font-semibold [&_h2]:text-sm [&_h2]:font-semibold [&_h3]:text-sm [&_h3]:font-semibold",
				"[&_a]:underline [&_a]:underline-offset-2",
				"[&_strong]:font-semibold",
				"[&_hr]:my-3 [&_hr]:border-current/15",
				"[&_blockquote]:border-l-2 [&_blockquote]:border-current/25 [&_blockquote]:pl-3 [&_blockquote]:italic",
				// Wide tables and long code samples scroll inside the bubble
				// rather than stretching it past the conversation.
				"[&_pre]:overflow-x-auto [&_pre]:rounded-xl [&_pre]:p-3 [&_pre]:text-xs",
				"[&_code]:rounded [&_code]:px-1 [&_code]:py-0.5 [&_code]:text-[0.85em]",
				"[&_pre_code]:bg-transparent [&_pre_code]:p-0",
				"[&_table]:block [&_table]:w-full [&_table]:overflow-x-auto [&_table]:text-xs",
				"[&_th]:px-2 [&_th]:py-1 [&_th]:text-left [&_th]:font-semibold",
				"[&_td]:px-2 [&_td]:py-1",
				onDark
					? "[&_code]:bg-white/20 [&_pre]:bg-white/15 [&_th]:border-b [&_th]:border-white/25 [&_td]:border-b [&_td]:border-white/10"
					: "[&_code]:bg-charcoal-blue-100 dark:[&_code]:bg-white/10 [&_pre]:bg-charcoal-blue-100 dark:[&_pre]:bg-white/10 [&_th]:border-b [&_th]:border-charcoal-blue-200 dark:[&_th]:border-white/15 [&_td]:border-b [&_td]:border-charcoal-blue-100 dark:[&_td]:border-white/10",
			)}
		>
			<Markdown
				remarkPlugins={[remarkGfm]}
				components={{
					// Anything the model links to is third-party by definition.
					a: ({ href, children }) => (
						<a href={href} target="_blank" rel="noopener noreferrer nofollow">
							{children}
						</a>
					),
				}}
			>
				{content}
			</Markdown>
		</div>
	);
}
