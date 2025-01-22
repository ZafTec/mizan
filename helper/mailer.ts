
import nodemailer from "nodemailer"
import SMTPTransport from "nodemailer/lib/smtp-transport"
import bcryptjs from "bcryptjs"
import UserModel from "@/model/user"
import { ID } from "@/types/id"


interface EmailProps {
	email: string,
	emailType: "VERIFY" | "RESET",
	userId: ID
}

const domain = process.env.DOMAIN || process.env.VERCEL_URL

// Verify email template html
const verifyEmailTemplate = (token: string) => {
	return `<p>Click <a href="${domain}/verifyemail?token=${token}">here</a> to verify your email</p>`
}

// Reset password template html
const resetPasswordTemplate = (token: string) => {
	return `<p>Click <a href="${domain}/resetpassword?token=${token}">here</a> to reset your password</p>`
}

export async function sendEmail({ email, emailType, userId }: EmailProps) {
	try {

		// Create a hash token based on the user's ID
		const hashedToken = await bcryptjs.hash(userId.toString(), 10)

		// Update the user document in the database with the generated token and expiry time
		if(emailType === "VERIFY") {
			await UserModel.findByIdAndUpdate(userId,
				{
					verifyToken: hashedToken,
					verifyTokenExpiry: Date.now() + 3600000
				},
			)
		} else if(emailType === "RESET") {
			await UserModel.findByIdAndUpdate(userId,
				{
					forgotPasswordToken: hashedToken,
					forgotPasswordTokenExpiry: Date.now() + 3600000
				},
			)
		}

		var transport = nodemailer.createTransport({
			host: process.env.NODE_MAILER_HOST!,
			port: Number(process.env.NODE_MAILER_PORT),
			auth: {
				user: process.env.NODE_MAILER_USER,
				pass: process.env.NODE_MAILER_PASS
			}
		} as SMTPTransport.Options);

		// Compose email options
		const mailOptions = {
			from: 'Macro <euaelesh@gmail.com>',
			to: email,
			subject: emailType === "VERIFY" ? "Verify your email" : "Reset your password",
			html: emailType === "VERIFY" ? verifyEmailTemplate(hashedToken) : resetPasswordTemplate(hashedToken)
		}

		// Send the email
		const mailresponse = await transport.sendMail(mailOptions);
		return mailresponse
	   
	} catch (error: any) {
		throw new Error(error.message);
	}
}
