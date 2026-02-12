import "server-only";
import { logger } from "@/lib/logger";

const startupLogger = logger.createModuleLogger("startup-validation");

export function validateStartupConfig(): void {
  const errors: string[] = [];
  const warnings: string[] = [];

  if (!process.env.DATABASE_URL) {
    errors.push("DATABASE_URL environment variable is required");
  }

  if (!process.env.BETTER_AUTH_SECRET) {
    errors.push("BETTER_AUTH_SECRET environment variable is required");
  }

  if (!process.env.BETTER_AUTH_URL) {
    warnings.push("BETTER_AUTH_URL not set, using default: http://localhost:3000");
  }

  if (!process.env.BETTER_AUTH_ISSUER) {
    warnings.push("BETTER_AUTH_ISSUER not set, using default: http://localhost:3000");
  }

  if (!process.env.BETTER_AUTH_AUDIENCE) {
    warnings.push("BETTER_AUTH_AUDIENCE not set, using default: http://localhost:3000");
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
