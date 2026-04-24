import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { HeroSection } from "@/components/Landing/HeroSection";
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
	title: "Mizan – Balanced nutrition, training, and coaching",
	description:
		"Mizan by Zaftech is the single workspace for meals, workouts, and coaching. Log fast, plan a week, ship goals. Free plan. Pro from $0.99 / month.",
	alternates: { canonical: siteUrl },
	openGraph: {
		type: "website",
		siteName: "Mizan",
		title: "Mizan – Balanced nutrition, training, and coaching",
		description:
			"One workspace for meals, workouts, and coaching. Free plan or Pro from $0.99 / month.",
		url: siteUrl,
	},
	twitter: {
		card: "summary_large_image",
		site: "@ZaftechS",
		creator: "@ZaftechS",
		title: "Mizan – Balanced nutrition, training, and coaching",
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
				{
					"@type": "Offer",
					name: "Free",
					price: "0",
					priceCurrency: "USD",
				},
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
				{
					"@type": "Offer",
					name: "Lifetime",
					price: "29",
					priceCurrency: "USD",
				},
			],
		},
	],
};

export default async function Home() {
	const user = await getUserOptionalServer();

	if (user) {
		redirect("/dashboard");
	}

	return (
		<>
			<script
				type="application/ld+json"
				dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData) }}
			/>
			<div className="space-y-12 sm:space-y-16">
				<HeroSection />
				<FeatureSection />
				<HowItWorksSection />
				<PricingSection />
				<TestimonialSection />
				<CTASection />
			</div>
		</>
	);
}
