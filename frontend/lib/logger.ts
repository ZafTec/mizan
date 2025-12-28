type LogLevel = "debug" | "info" | "warn" | "error";

interface LogMetadata {
  [key: string]: any;
}

interface LogContext {
  timestamp: string;
  level: LogLevel;
  module: string;
  message: string;
  metadata?: LogMetadata;
}

class Logger {
  private isDevelopment: boolean;
  private isServer: boolean;

  constructor() {
    this.isDevelopment = process.env.NODE_ENV !== "production";
    this.isServer = typeof window === "undefined";
  }

  private shouldLog(level: LogLevel): boolean {
    if (this.isDevelopment) return true;

    const productionLevels: LogLevel[] = ["warn", "error"];
    return productionLevels.includes(level);
  }

  private formatMessage(context: LogContext): string {
    const { timestamp, level, module, message, metadata } = context;
    const prefix = `[${timestamp}] [${level.toUpperCase()}] [${module}]`;

    if (!metadata || Object.keys(metadata).length === 0) {
      return `${prefix} ${message}`;
    }

    const sanitized = this.sanitizeMetadata(metadata);
    return `${prefix} ${message} ${JSON.stringify(sanitized)}`;
  }

  private sanitizeMetadata(metadata: LogMetadata): LogMetadata {
    const sensitiveKeys = [
      "password",
      "token",
      "secret",
      "authorization",
      "cookie",
      "apiKey",
      "api_key",
      "bearer",
    ];

    const sanitized: LogMetadata = {};

    for (const [key, value] of Object.entries(metadata)) {
      const lowerKey = key.toLowerCase();

      if (sensitiveKeys.some((sensitive) => lowerKey.includes(sensitive))) {
        sanitized[key] = "[REDACTED]";
      } else if (typeof value === "object" && value !== null) {
        sanitized[key] = this.sanitizeMetadata(value);
      } else {
        sanitized[key] = value;
      }
    }

    return sanitized;
  }

  private log(
    level: LogLevel,
    module: string,
    message: string,
    metadata?: LogMetadata
  ): void {
    if (!this.shouldLog(level)) return;

    const context: LogContext = {
      timestamp: new Date().toISOString(),
      level,
      module,
      message,
      metadata,
    };

    const formatted = this.formatMessage(context);

    switch (level) {
      case "debug":
        console.debug(formatted);
        break;
      case "info":
        console.info(formatted);
        break;
      case "warn":
        console.warn(formatted);
        break;
      case "error":
        console.error(formatted);
        break;
    }
  }

  debug(module: string, message: string, metadata?: LogMetadata): void {
    this.log("debug", module, message, metadata);
  }

  info(module: string, message: string, metadata?: LogMetadata): void {
    this.log("info", module, message, metadata);
  }

  warn(module: string, message: string, metadata?: LogMetadata): void {
    this.log("warn", module, message, metadata);
  }

  error(module: string, message: string, metadata?: LogMetadata): void {
    this.log("error", module, message, metadata);
  }

  createModuleLogger(module: string) {
    return {
      debug: (message: string, metadata?: LogMetadata) =>
        this.debug(module, message, metadata),
      info: (message: string, metadata?: LogMetadata) =>
        this.info(module, message, metadata),
      warn: (message: string, metadata?: LogMetadata) =>
        this.warn(module, message, metadata),
      error: (message: string, metadata?: LogMetadata) =>
        this.error(module, message, metadata),
    };
  }
}

export const logger = new Logger();
