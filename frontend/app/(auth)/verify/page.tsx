export default async function Page({ searchParams }: { searchParams: Promise<{ email?: string }> }) {
	const { email } = await searchParams;

	if (!email || typeof email !== 'string') {
		return (
			<div className="min-h-[60vh] flex flex-col items-center justify-center">
				<div className="card p-8 max-w-md w-full text-center">
					<div className="w-16 h-16 rounded-2xl bg-red-100 flex items-center justify-center mx-auto mb-4">
						<i className="ri-error-warning-line text-3xl text-red-600" />
					</div>
					<h1 className="text-xl font-bold text-slate-900 mb-2">Error</h1>
					<p className="text-slate-600">
						No email address provided. Please go back and enter a valid email address.
					</p>
				</div>
			</div>
		);
	}

	return (
		<div className="min-h-[60vh] flex flex-col items-center justify-center">
			<div className="card p-8 max-w-md w-full text-center">
				<div className="w-16 h-16 rounded-2xl bg-brand-100 flex items-center justify-center mx-auto mb-4">
					<i className="ri-mail-check-line text-3xl text-brand-600" />
				</div>
				<h1 className="text-xl font-bold text-slate-900 mb-2">Verify Your Email Address</h1>
				<p className="text-slate-600 mb-4">
					Thank you for registering! A verification email has been sent to{' '}
					<span className="font-medium text-slate-900">{email}</span>.
				</p>
				<p className="text-slate-600 mb-6">
					Please check your inbox and click on the verification link to activate your account.
				</p>
				<p className="text-sm text-slate-500">
					Didn&apos;t receive the email? Check your spam folder or{' '}
					<a
						href={`/verify?email=${encodeURIComponent(email)}`}
						className="text-brand-600 hover:underline"
					>
						click here to resend
					</a>
					.
				</p>
			</div>
		</div>
	);
}
