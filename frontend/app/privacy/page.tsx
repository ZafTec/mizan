import Link from "next/link";

export const metadata = {
	title: "Privacy Policy | Mizan",
	description: "Learn how Mizan collects, uses, and protects your personal information.",
};

export default function PrivacyPage() {
	const lastUpdated = "December 12, 2025";

	return (
		<div className="max-w-4xl mx-auto space-y-8">
			{/* Header */}
			<div>
				<Link href="/" className="inline-flex items-center gap-2 text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 mb-4">
					<i className="ri-arrow-left-line" />
					Back to Home
				</Link>
				<h1 className="text-4xl font-bold text-slate-900 dark:text-slate-100 mb-2">Privacy Policy</h1>
				<p className="text-slate-500 dark:text-slate-400">Last updated: {lastUpdated}</p>
			</div>

			{/* Content */}
			<div className="card p-8 prose prose-slate max-w-none">
				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">1. Introduction</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Welcome to Mizan ("we," "our," or "us"). We are committed to protecting your privacy and personal information. This Privacy Policy explains how we collect, use, disclose, and safeguard your information when you use our nutrition tracking and meal planning application.
					</p>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						By using Mizan, you agree to the collection and use of information in accordance with this policy. If you do not agree with our policies and practices, please do not use our service.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">2. Information We Collect</h2>

					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">2.1 Personal Information</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">When you create an account, we collect:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Email address</li>
						<li>Name</li>
						<li>Password (encrypted)</li>
						<li>Profile information you choose to provide</li>
					</ul>

					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">2.2 Health and Nutrition Data</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">To provide our services, we collect:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Food diary entries and meal logs</li>
						<li>Nutritional goals and preferences</li>
						<li>Body measurements and weight tracking data</li>
						<li>Exercise and activity information</li>
						<li>Health goals and fitness objectives</li>
					</ul>

					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">2.3 Usage Information</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">We automatically collect:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Device information (browser type, operating system)</li>
						<li>IP address and location data</li>
						<li>Usage patterns and feature interactions</li>
						<li>Session information and timestamps</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">3. How We Use Your Information</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">We use the collected information to:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Provide and maintain our nutrition tracking services</li>
						<li>Generate personalized meal plans and recommendations</li>
						<li>Calculate and track your nutritional intake and goals</li>
						<li>Provide AI-powered coaching and suggestions</li>
						<li>Improve and optimize our application</li>
						<li>Send important service updates and notifications</li>
						<li>Respond to your support requests and feedback</li>
						<li>Ensure the security and integrity of our services</li>
						<li>Comply with legal obligations</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">4. Data Sharing and Disclosure</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We do not sell your personal information. We may share your information in the following circumstances:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li><strong>Service Providers:</strong> We may share data with trusted third-party service providers who assist in operating our application (hosting, analytics, AI services)</li>
						<li><strong>Legal Requirements:</strong> When required by law, court order, or government request</li>
						<li><strong>Business Transfers:</strong> In connection with any merger, sale, or acquisition of our company</li>
						<li><strong>With Your Consent:</strong> When you explicitly authorize us to share your information</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">5. Data Security</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We implement industry-standard security measures to protect your personal information:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Encryption of data in transit (HTTPS/TLS)</li>
						<li>Encryption of sensitive data at rest</li>
						<li>Secure password hashing using industry best practices</li>
						<li>Regular security assessments and updates</li>
						<li>Access controls and authentication mechanisms</li>
					</ul>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed mt-4">
						However, no method of transmission over the Internet is 100% secure. While we strive to protect your personal information, we cannot guarantee absolute security.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">6. Your Rights and Choices</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">You have the right to:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li><strong>Access:</strong> Request a copy of your personal data</li>
						<li><strong>Correction:</strong> Update or correct inaccurate information</li>
						<li><strong>Deletion:</strong> Request deletion of your account and associated data</li>
						<li><strong>Export:</strong> Download your data in a portable format</li>
						<li><strong>Opt-out:</strong> Unsubscribe from marketing communications</li>
						<li><strong>Restrict Processing:</strong> Limit how we use your data</li>
					</ul>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed mt-4">
						To exercise these rights, please contact us using the information provided below.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">7. Data Retention</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We retain your personal information for as long as necessary to provide our services and comply with legal obligations. When you delete your account, we will delete or anonymize your personal data within 30 days, except where we are legally required to retain certain information.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">8. Children's Privacy</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Our service is not intended for children under 13 years of age. We do not knowingly collect personal information from children under 13. If you believe we have collected information from a child under 13, please contact us immediately.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">9. International Data Transfers</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Your information may be transferred to and maintained on servers located outside of your state, province, country, or other governmental jurisdiction where data protection laws may differ. By using our service, you consent to such transfers.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">10. Changes to This Privacy Policy</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We may update this Privacy Policy from time to time. We will notify you of any changes by posting the new Privacy Policy on this page and updating the "Last updated" date. We encourage you to review this Privacy Policy periodically for any changes.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">11. Contact Us</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						If you have any questions about this Privacy Policy or our data practices, please contact us:
					</p>
					<div className="mt-4 p-4 bg-slate-50 dark:bg-slate-800 rounded-lg">
						<p className="text-slate-700 dark:text-slate-300"><strong>Email:</strong> privacy@mizan.app</p>
						<p className="text-slate-700 dark:text-slate-300 mt-2"><strong>Website:</strong> <a href="https://mizan.app" className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300">mizan.app</a></p>
					</div>
				</section>
			</div>
		</div>
	);
}
