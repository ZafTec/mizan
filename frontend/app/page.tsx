import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { HeroSection } from "@/components/Landing/HeroSection";
import { ThreeDoorsSection } from "@/components/Landing/ThreeDoorsSection";
import { FaqSection } from "@/components/Landing/FaqSection";
import { CoachingSection } from "@/components/Landing/CoachingSection";
import { PricingSection } from "@/components/Landing/PricingSection";
import { CTASection } from "@/components/Landing/CTASection";
import { getUserOptionalServer } from "@/helper/session";

// Authed users never see this page, they land on /dashboard. Keeping this as
// a purely marketing route lets us keep metadata, structured data, and hero
// copy free of "logged-in" branching noise.
export const dynamic = "force-dynamic";

const siteUrl = "https://mizan.zaftech.co";

export const metadata: Metadata = {
	title: "Mizan | The tracker you actually keep using.",
	description:
		"Mizan by Zaftech logs meals, workouts and measurements from the web, Telegram, or your AI assistant. Free forever, Pro from $2.99/mo, or self-host it yourself.",
	alternates: { canonical: siteUrl },
	openGraph: {
		type: "website",
		siteName: "Mizan",
		title: "Mizan | The tracker you actually keep using.",
		description: "One log, three doors in: the web app, Telegram, or your AI assistant. Free forever, Pro from $2.99/mo, or self-host it.",
		url: siteUrl,
	},
	twitter: {
		card: "summary_large_image",
		site: "@ZaftechS",
		creator: "@ZaftechS",
		title: "Mizan | The tracker you actually keep using.",
		description: "One log, three doors in: the web app, Telegram, or your AI assistant.",
	},
};

const structuredData = {
	"@context": "https://schema.org",
	"@graph": [
		{
			"@type": "Organization",
			"@id": "https://zaftech.co/#organization",
			name: "Zaftech",
			url: "https://zaftech.co",
			sameAs: ["https://x.com/ZaftechS", "https://www.youtube.com/@Zaftec"],
		},
		{
			"@type": "SoftwareApplication",
			name: "Mizan",
			applicationCategory: "HealthApplication",
			operatingSystem: "Web",
			url: siteUrl,
			publisher: { "@id": "https://zaftech.co/#organization" },
			offers: [
				{ "@type": "Offer", name: "Free", price: "0", priceCurrency: "USD" },
				{
					"@type": "Offer",
					name: "Pro",
					price: "2.99",
					priceCurrency: "USD",
					priceSpecification: {
						"@type": "UnitPriceSpecification",
						price: "2.99",
						priceCurrency: "USD",
						billingDuration: "P1M",
					},
				},
				{ "@type": "Offer", name: "Self-hosted", price: "0", priceCurrency: "USD" },
			],
		},
	],
};

export default async function Home() {
	const user = await getUserOptionalServer();
	if (user) redirect("/dashboard");

	return (
		<>
			<script
				type="application/ld+json"
				dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData) }}
			/>
			<div>
				<HeroSection />
				<ThreeDoorsSection />
				<FaqSection />
				<CoachingSection />
				<PricingSection />
				<CTASection />
			</div>
		</>
	);
}
