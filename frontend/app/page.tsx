import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { HeroSection } from "@/components/Landing/HeroSection";
import { MetricsTicker } from "@/components/Landing/MetricsTicker";
import { FeatureSection } from "@/components/Landing/FeatureSection";
import { HowItWorksSection } from "@/components/Landing/HowItWorksSection";
import { PricingSection } from "@/components/Landing/PricingSection";
import { TestimonialSection } from "@/components/Landing/TestimonialCard";
import { CTASection } from "@/components/Landing/CTASection";
import { getUserOptionalServer } from "@/helper/session";

// Authed users never see this page — they land on /dashboard. Keeping this as
// a purely marketing route lets us keep metadata, structured data, and hero
// copy free of "logged-in" branching noise.
export const dynamic = "force-dynamic";

const siteUrl = "https://mizan.zaftech.co";

export const metadata: Metadata = {
	title: "Mizan – Your macros. Surgical.",
	description:
		"Mizan by Zaftech is the nutrition app built like a HUD, not a spreadsheet. Track, plan, and ship goals. Free plan, or Pro from $0.99 / month.",
	alternates: { canonical: siteUrl },
	openGraph: {
		type: "website",
		siteName: "Mizan",
		title: "Mizan – Your macros. Surgical.",
		description: "One workspace for meals, workouts, and coaching. Free plan or Pro from $0.99 / month.",
		url: siteUrl,
	},
	twitter: {
		card: "summary_large_image",
		site: "@ZaftechS",
		creator: "@ZaftechS",
		title: "Mizan – Your macros. Surgical.",
		description: "One workspace for meals, workouts, and coaching.",
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
			sameAs: [
				"https://x.com/ZaftechS",
				"https://www.youtube.com/@Zaftec",
			],
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
					price: "0.99",
					priceCurrency: "USD",
					priceSpecification: {
						"@type": "UnitPriceSpecification",
						price: "0.99",
						priceCurrency: "USD",
						billingDuration: "P1M",
					},
				},
				{ "@type": "Offer", name: "Lifetime", price: "29", priceCurrency: "USD" },
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
			{/* Negative margins break out of the root layout's container so the
			    dark Ethereal Lab surface fills viewport width. The inner container
			    restores content max-width. */}
			<div className="eth-lab -mx-4 -my-6 min-h-screen sm:-mx-6 sm:-my-8 lg:-mx-8 lg:-my-10">
				<div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8 lg:py-10">
					<HeroSection />
				</div>

				<MetricsTicker />

				<div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
					<FeatureSection />
					<HowItWorksSection />
					<TestimonialSection />
					<PricingSection />
					<CTASection />
				</div>
			</div>
		</>
	);
}
