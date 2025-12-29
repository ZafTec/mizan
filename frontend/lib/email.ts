import "server-only";
import nodemailer from "nodemailer";
import { logger } from "@/lib/logger";

const emailLogger = logger.createModuleLogger("email-service");

interface EmailOptions {
  to: string;
  subject: string;
  html: string;
  text?: string;
}

// Create transporter based on environment
const createTransporter = () => {
  // In production, use real SMTP
  if (process.env.NODE_ENV === "production") {
    const port = parseInt(process.env.SMTP_PORT || "587");
    return nodemailer.createTransport({
      host: process.env.SMTP_HOST,
      port: port,
      secure: port === 465, // true for 465 (SSL), false for 587 (STARTTLS) and other ports
      auth: {
        user: process.env.SMTP_USER,
        pass: process.env.SMTP_PASS,
      },
    });
  }

  if (process.env.SMTP_HOST && process.env.SMTP_USER && process.env.SMTP_PASS) {
    const port = parseInt(process.env.SMTP_PORT || "587");
    return nodemailer.createTransport({
      host: process.env.SMTP_HOST,
      port: port,
      secure: port === 465, // true for 465 (SSL), false for 587 (STARTTLS) and other ports
      auth: {
        user: process.env.SMTP_USER,
        pass: process.env.SMTP_PASS,
      },
    });
  }
  return null;
};

export async function sendEmail(options: EmailOptions) {
  const transporter = createTransporter();

  if (!transporter) {
    emailLogger.error("Email attempted with no SMTP configured", { email: options.to });
    return;
  }

  try {
    const info = await transporter.sendMail({
      from: process.env.SMTP_FROM || process.env.SMTP_USER,
      to: options.to,
      subject: options.subject,
      text: options.text,
      html: options.html,
    });
    emailLogger.info("Email sent successfully", { email: options.to, messageId: info.messageId });
  } catch (error) {
    emailLogger.error("Failed to send email", {
      email: options.to,
      error: error instanceof Error ? error.message : String(error),
    });
    throw error;
  }
}

// Email templates
export function getVerificationEmailTemplate(url: string, userName?: string) {
  return {
    subject: "Verify your email address - Mizan",
    html: `
      <!DOCTYPE html>
      <html>
        <head>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; }
            .container { max-width: 600px; margin: 0 auto; padding: 20px; }
            .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
            .content { background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }
            .button { display: inline-block; background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; margin: 20px 0; }
            .footer { text-align: center; margin-top: 30px; color: #6b7280; font-size: 14px; }
          </style>
        </head>
        <body>
          <div class="container">
            <div class="header">
              <h1>Welcome to Mizan!</h1>
            </div>
            <div class="content">
              <p>Hi${userName ? ` ${userName}` : ""},</p>
              <p>Thank you for signing up! Please verify your email address to get started with your nutrition journey.</p>
              <p style="text-align: center;">
                <a href="${url}" class="button">Verify Email Address</a>
              </p>
              <p style="color: #6b7280; font-size: 14px;">
                Or copy and paste this link into your browser:<br>
                <a href="${url}" style="color: #667eea; word-break: break-all;">${url}</a>
              </p>
              <p style="color: #6b7280; font-size: 14px; margin-top: 30px;">
                If you didn't create an account, you can safely ignore this email.
              </p>
            </div>
            <div class="footer">
              <p>© 2025 Mizan. All rights reserved.</p>
            </div>
          </div>
        </body>
      </html>
    `,
    text: `Welcome to Mizan!\n\nPlease verify your email address by clicking the link below:\n\n${url}\n\nIf you didn't create an account, you can safely ignore this email.`,
  };
}

export function getPasswordResetEmailTemplate(url: string, userName?: string) {
  return {
    subject: "Reset your password - Mizan",
    html: `
      <!DOCTYPE html>
      <html>
        <head>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; }
            .container { max-width: 600px; margin: 0 auto; padding: 20px; }
            .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
            .content { background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }
            .button { display: inline-block; background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; margin: 20px 0; }
            .footer { text-align: center; margin-top: 30px; color: #6b7280; font-size: 14px; }
            .warning { background: #fef3c7; border-left: 4px solid #f59e0b; padding: 15px; margin: 20px 0; border-radius: 4px; }
          </style>
        </head>
        <body>
          <div class="container">
            <div class="header">
              <h1>Reset Your Password</h1>
            </div>
            <div class="content">
              <p>Hi${userName ? ` ${userName}` : ""},</p>
              <p>We received a request to reset your password for your Mizan account. Click the button below to create a new password:</p>
              <p style="text-align: center;">
                <a href="${url}" class="button">Reset Password</a>
              </p>
              <p style="color: #6b7280; font-size: 14px;">
                Or copy and paste this link into your browser:<br>
                <a href="${url}" style="color: #667eea; word-break: break-all;">${url}</a>
              </p>
              <div class="warning">
                <strong>⚠️ Security Notice</strong><br>
                This link will expire in 1 hour. If you didn't request a password reset, please ignore this email or contact support if you have concerns.
              </div>
            </div>
            <div class="footer">
              <p>© 2025 Mizan. All rights reserved.</p>
            </div>
          </div>
        </body>
      </html>
    `,
    text: `Reset Your Password\n\nWe received a request to reset your password for your Mizan account.\n\nClick the link below to create a new password:\n\n${url}\n\nThis link will expire in 1 hour.\n\nIf you didn't request a password reset, please ignore this email.`,
  };
}
