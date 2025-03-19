
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

const domain = process.env.DOMAIN

// Verify email template html
const verifyEmailTemplate = (token: string) => {
	return `
		<!DOCTYPE html>
		<html>
			<head>
				<title>Verify Your Email</title>
				<meta charset="UTF-8">
				<style>
					/* Fallback font styles */
					body, table, td, a {
						-webkit-text-size-adjust: 100%; 
						-ms-text-size-adjust: 100%; 
					}
					table, td {
						mso-table-lspace: 0pt;
						mso-table-rspace: 0pt;
					}
					img {
						-ms-interpolation-mode: bicubic;
					}
					body {
						margin: 0;
						padding: 0;
						width: 100% !important;
						height: 100% !important;
					}
					a[x-apple-data-detectors] {
						color: inherit !important;
						text-decoration: none !important;
					}
				</style>
			</head>
			<body style="margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;">
				<table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;">
					<!-- Logo -->
					<tr>
						<td align="center" bgcolor="#70bbd9" style="padding: 40px 0 30px 0;">
							<img src="${domain}/logo.png" alt="Macro-Chef Logo" width="200" style="display: block;" />
						</td>
					</tr>
					<!-- Title -->
					<tr>
						<td bgcolor="#ffffff" align="center" style="padding: 20px 30px 20px 30px;">
							<h1 style="font-size:24px; margin:0;">Welcome to Our Service!</h1>
						</td>
					</tr>
					<!-- Content -->
					<tr>
						<td bgcolor="#ffffff" style="padding: 20px 30px 40px 30px; color:#333333; font-size:16px; line-height:1.5em;">
							<p style="margin:0;">Hi there,</p>
							<p>Thank you for signing up. Please verify your email address by clicking the button below.</p>
							<p style="text-align:center; margin: 30px 0;">
							<a href="${domain}/verifyemail?token=${token}" style="background-color:#4CAF50; color:#ffffff; padding: 15px 25px; text-decoration: none; font-size:16px; border-radius:5px;">Verify Email</a>
							</p>
							<p>If you did not sign up for an account, please ignore this email.</p>
							<p>Best regards,<br/>The Team</p>
						</td>
					</tr>
					<!-- Footer -->
					<tr>
						<td bgcolor="#f4f4f4" style="padding: 30px 30px 30px 30px; text-align:center; color:#777777; font-size:12px;">
							&copy; ${new Date().getFullYear()} Macro Chef. All rights reserved.<br/>
							<a href="${domain}/privacy" style="color:#777777; text-decoration:underline;">Privacy Policy</a> | <a href="${domain}/terms" style="color:#777777; text-decoration:underline;">Terms of Service</a>
						</td>
					</tr>
				</table>
			</body>
		</html>
	`;
}
  

// Reset password template html
const resetPasswordTemplate = (token: string) => {
	return `
	<!DOCTYPE html>
		<html>
			<head>
				<title>Reset Your Password</title>
				<meta charset="UTF-8">
				<style>
					/* Fallback font styles */
					body, table, td, a {
						-webkit-text-size-adjust: 100%; 
						-ms-text-size-adjust: 100%; 
					}
					table, td {
						mso-table-lspace: 0pt;
						mso-table-rspace: 0pt;
					}
					img {
						-ms-interpolation-mode: bicubic;
					}
					body {
						margin: 0;
						padding: 0;
						width: 100% !important;
						height: 100% !important;
					}
					a[x-apple-data-detectors] {
						color: inherit !important;
						text-decoration: none !important;
					}
				</style>
			</head>
			<body style="margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;">
				<table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;">
					<!-- Logo -->
					<tr>
						<td align="center" bgcolor="#70bbd9" style="padding: 40px 0 30px 0;">
							<img src="${domain}/logo.png" alt="Macro Chef Logo" width="200" style="display: block;" />
						</td>
					</tr>
					<!-- Title -->
					<tr>
						<td bgcolor="#ffffff" align="center" style="padding: 20px 30px 20px 30px;">
							<h1 style="font-size:24px; margin:0;">Reset Your Password</h1>
						</td>
					</tr>
					<!-- Content -->
					<tr>
						<td bgcolor="#ffffff" style="padding: 20px 30px 40px 30px; color:#333333; font-size:16px; line-height:1.5em;">
							<p style="margin:0;">Hi there,</p>
							<p>We received a request to reset your password. Click the button below to reset it.</p>
							<p style="text-align:center; margin: 30px 0;">
							<a href="${domain}/resetpassword?token=${token}" style="background-color:#f44336; color:#ffffff; padding: 15px 25px; text-decoration: none; font-size:16px; border-radius:5px;">Reset Password</a>
							</p>
							<p>If you did not request a password reset, please ignore this email or contact support if you have questions.</p>
							<p>Best regards,<br/>The Team</p>
						</td>
					</tr>
					<!-- Footer -->
					<tr>
						<td bgcolor="#f4f4f4" style="padding: 30px 30px 30px 30px; text-align:center; color:#777777; font-size:12px;">
							&copy; ${new Date().getFullYear()} Macro Chef. All rights reserved.<br/>
							<a href="${domain}/privacy" style="color:#777777; text-decoration:underline;">Privacy Policy</a> | <a href="${domain}/terms" style="color:#777777; text-decoration:underline;">Terms of Service</a>
						</td>
					</tr>
				</table>
			</body>
		</html>
	`;
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

		const transport = nodemailer.createTransport({
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
