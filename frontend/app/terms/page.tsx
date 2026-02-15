import Link from "next/link";

export const metadata = {
	title: "Terms of Service | Mizan",
	description: "Terms and conditions for using Mizan nutrition tracking application.",
};

export default function TermsPage() {
	const lastUpdated = "December 12, 2025";

	return (
		<div className="max-w-4xl mx-auto space-y-8">
			{/* Header */}
			<div>
				<Link href="/" className="inline-flex items-center gap-2 text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 mb-4">
					<i className="ri-arrow-left-line" />
					Back to Home
				</Link>
				<h1 className="text-4xl font-bold text-slate-900 dark:text-slate-100 mb-2">Terms of Service</h1>
				<p className="text-slate-500 dark:text-slate-400">Last updated: {lastUpdated}</p>
			</div>

			{/* Content */}
			<div className="card p-8 prose prose-slate max-w-none">
				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">1. Acceptance of Terms</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Welcome to Mizan. By accessing or using our nutrition tracking and meal planning application ("Service"), you agree to be bound by these Terms of Service ("Terms"). If you do not agree to these Terms, please do not use our Service.
					</p>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We reserve the right to modify these Terms at any time. Your continued use of the Service after changes are posted constitutes acceptance of the modified Terms.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">2. Description of Service</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Mizan is a nutrition tracking and meal planning application that helps users:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Track food intake and nutritional data</li>
						<li>Plan meals and manage recipes</li>
						<li>Set and monitor health and fitness goals</li>
						<li>Receive AI-powered personalized recommendations</li>
						<li>Monitor progress through analytics and insights</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">3. User Accounts and Registration</h2>
					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">3.1 Account Creation</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						To use certain features of our Service, you must create an account. You agree to:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Provide accurate, current, and complete information</li>
						<li>Maintain and update your information to keep it accurate</li>
						<li>Maintain the security of your account credentials</li>
						<li>Notify us immediately of any unauthorized access</li>
						<li>Be responsible for all activities under your account</li>
					</ul>

					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">3.2 Eligibility</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						You must be at least 13 years old to use our Service. If you are under 18, you must have parental or guardian consent.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">4. Acceptable Use</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">You agree not to:</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Violate any applicable laws or regulations</li>
						<li>Infringe on intellectual property rights</li>
						<li>Upload malicious code, viruses, or harmful content</li>
						<li>Attempt to gain unauthorized access to our systems</li>
						<li>Use automated systems (bots, scrapers) without permission</li>
						<li>Interfere with or disrupt the Service</li>
						<li>Impersonate others or provide false information</li>
						<li>Harass, abuse, or harm other users</li>
						<li>Use the Service for commercial purposes without authorization</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">5. Medical Disclaimer</h2>
					<div className="p-4 bg-amber-50 dark:bg-amber-950 border-l-4 border-amber-500 rounded">
						<p className="text-slate-700 dark:text-slate-300 leading-relaxed font-semibold mb-2">
							IMPORTANT: The Service is not a substitute for professional medical advice, diagnosis, or treatment.
						</p>
						<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
							Mizan provides general nutrition information and tracking tools. Always seek the advice of your physician or qualified health provider with any questions regarding a medical condition, diet changes, or fitness program. Never disregard professional medical advice or delay seeking it because of information from our Service.
						</p>
					</div>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed mt-4">
						The nutritional data, calorie calculations, and recommendations provided by Mizan are estimates and may not be accurate for all individuals. We are not responsible for any health issues that may arise from using our Service.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">6. User Content</h2>
					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">6.1 Your Content</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						You retain ownership of any content you submit to the Service (recipes, meal logs, etc.). By submitting content, you grant us a worldwide, non-exclusive, royalty-free license to use, store, display, and distribute your content as necessary to provide the Service.
					</p>

					<h3 className="text-xl font-semibold text-slate-800 dark:text-slate-200 mb-3 mt-6">6.2 Content Standards</h3>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						You are solely responsible for your content. You agree that your content will not:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Violate any laws or third-party rights</li>
						<li>Contain false, misleading, or harmful information</li>
						<li>Include offensive, inappropriate, or adult content</li>
						<li>Promote dangerous eating behaviors or disorders</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">7. Intellectual Property</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						The Service, including all content, features, and functionality, is owned by Mizan and protected by copyright, trademark, and other intellectual property laws. You may not copy, modify, distribute, sell, or lease any part of our Service without our express written permission.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">8. Third-Party Services and Links</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						Our Service may contain links to third-party websites or services that are not owned or controlled by Mizan. We are not responsible for the content, privacy policies, or practices of any third-party sites or services.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">9. Disclaimer of Warranties</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						THE SERVICE IS PROVIDED "AS IS" AND "AS AVAILABLE" WITHOUT WARRANTIES OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Accuracy, reliability, or completeness of information</li>
						<li>Uninterrupted or error-free operation</li>
						<li>Fitness for a particular purpose</li>
						<li>Non-infringement</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">10. Limitation of Liability</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						TO THE MAXIMUM EXTENT PERMITTED BY LAW, MIZAN SHALL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR ANY LOSS OF PROFITS OR REVENUES, WHETHER INCURRED DIRECTLY OR INDIRECTLY, OR ANY LOSS OF DATA, USE, GOODWILL, OR OTHER INTANGIBLE LOSSES RESULTING FROM:
					</p>
					<ul className="list-disc list-inside text-slate-600 dark:text-slate-400 space-y-2 ml-4">
						<li>Your use or inability to use the Service</li>
						<li>Any unauthorized access to your data</li>
						<li>Any errors or omissions in content</li>
						<li>Any health issues arising from use of the Service</li>
					</ul>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">11. Indemnification</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						You agree to indemnify and hold harmless Mizan, its affiliates, and their respective officers, directors, employees, and agents from any claims, liabilities, damages, losses, and expenses arising from your use of the Service or violation of these Terms.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">12. Termination</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We reserve the right to suspend or terminate your account at any time, with or without notice, for conduct that we believe violates these Terms or is harmful to other users, us, or third parties, or for any other reason.
					</p>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						You may terminate your account at any time by contacting us. Upon termination, your right to use the Service will immediately cease.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">13. Governing Law</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						These Terms shall be governed by and construed in accordance with the laws of the jurisdiction in which Mizan operates, without regard to conflict of law principles.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">14. Changes to Terms</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						We reserve the right to modify or replace these Terms at any time. If a revision is material, we will provide at least 30 days' notice before new terms take effect. Your continued use of the Service after changes become effective constitutes acceptance of the revised Terms.
					</p>
				</section>

				<section className="mb-8">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-4">15. Contact Information</h2>
					<p className="text-slate-600 dark:text-slate-400 leading-relaxed">
						If you have any questions about these Terms, please contact us:
					</p>
					<div className="mt-4 p-4 bg-slate-50 dark:bg-slate-800 rounded-lg">
						<p className="text-slate-700 dark:text-slate-300"><strong>Email:</strong> legal@mizan.app</p>
						<p className="text-slate-700 dark:text-slate-300 mt-2"><strong>Website:</strong> <a href="https://mizan.app" className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300">mizan.app</a></p>
					</div>
				</section>
			</div>
		</div>
	);
}
