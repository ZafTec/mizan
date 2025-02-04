
import { resendUserVerificationEmail } from "@/data/user";

type Props = {
//   searchParams?: { [key: string]: string | string[] } | Promise<{ [key: string]: string | string[] }>;
	searchParams?: any;
}

export default async function Page({ searchParams }: Props) {
	const { email } = await searchParams || {};

	if (!email || typeof email !== 'string') {
		// Handle the case where the email is not provided
		return (
			<div className="flex flex-col items-center justify-center min-h-screen p-4 bg-gray-50">
				<h1 className="text-3xl font-bold mb-4 text-red-600">Error</h1>
				<p className="text-lg text-gray-700">
					No email address provided. Please go back and enter a valid email address.
				</p>
			</div>
		)
	}

	try {
		await resendUserVerificationEmail(email);
	} catch (error: any) {
		// Handle any errors that occurred during resendUserVerificationEmail
		console.error("Error resending verification email:", error);

		return (
			<div className="flex flex-col items-center justify-center min-h-screen p-4 bg-gray-50">
				<h1 className="text-3xl font-bold mb-4 text-red-600">Error</h1>
				<p className="text-lg text-gray-700">
					Failed to resend verification email. Please try again later or contact support.
				</p>
			</div>
		)
	}

	return (
		<div className="flex flex-col items-center min-h-screen p-4 bg-gray-50">
			<div className="bg-white shadow-lg rounded-lg p-8 max-w-md w-full text-center">
				<h1 className="text-2xl font-semibold mb-6 text-gray-800">Verify Your Email Address</h1>
				<p className="text-gray-700 mb-6">
					Thank you for registering! A new verification email has been sent to{' '}
					<span className="font-medium">{email}</span>.
				</p>
				<p className="text-gray-700 mb-6">
					Please check your inbox and click on the verification link to activate your account.
				</p>
				<p className="text-sm text-gray-600">
					Didn&apos;t receive the email? Check your spam folder or{' '}
					<a
						href={`/verify?email=${encodeURIComponent(email)}`}
						className="text-blue-600 hover:underline"
					>
						click here to resend
					</a>
					.
				</p>
			</div>
		</div>
	)
}
