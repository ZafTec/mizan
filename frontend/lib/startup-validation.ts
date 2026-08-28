import "server-only";
import { logger } from "@/lib/logger";

const startupLogger = logger.createModuleLogger("startup-validation");

export function validateStartupConfig(): void {
  const errors: string[] = [];
  const warnings: string[] = [];

  // The frontend holds no secrets since v2: no database, no signing key, no
  // SMTP credentials. It needs to know where the API is and nothing else.
  if (!process.env.NEXT_PUBLIC_API_URL) {
    warnings.push("NEXT_PUBLIC_API_URL not set; browser calls will default to http://localhost:5000");
  }

  if (!process.env.API_URL) {
    warnings.push("API_URL not set, using default: http://backend:8080");
  }

  if (errors.length > 0) {
    startupLogger.error("Critical startup configuration errors", { errors });
    throw new Error(
      `CRITICAL STARTUP FAILURE - Missing required environment variables:\n${errors.map(e => `  - ${e}`).join('\n')}\n\nThe application cannot start without these variables.`
    );
  }

  if (warnings.length > 0) {
    startupLogger.warn("Non-critical startup warnings", { warnings });
  }

  startupLogger.info("Startup configuration validated successfully");
}
