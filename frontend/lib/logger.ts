import pino, { Logger as PinoLogger, LoggerOptions } from "pino";

type LogLevel = "fatal" | "error" | "warn" | "info" | "debug" | "trace";

const isServer = typeof window === "undefined";
const isProd = process.env.NODE_ENV === "production";
const level: LogLevel = (process.env.LOG_LEVEL as LogLevel) || (isProd ? "info" : "debug");

const redactionPaths = [
  "*.password",
  "*.token",
  "*.secret",
  "*.authorization",
  "*.cookie",
  "*.apiKey",
  "*.api_key",
  "*.bearer",
  "headers.cookie",
];

const baseOptions: LoggerOptions = {
  level,
  redact: { paths: redactionPaths, censor: "[REDACTED]" },
};

const loggerInstance: PinoLogger = (() => {
  if (isServer) {
    // Pretty-print only in local/dev; JSON in prod to feed centralized logging.
    const transport = isProd
      ? undefined
      : {
          target: "pino-pretty",
          options: { colorize: true, translateTime: "SYS:standard", singleLine: true },
        };

    return pino({ ...baseOptions, transport });
  }

  // Browser: avoid base pid/hostname noise, emit objects for console grouping.
  return pino({
    ...baseOptions,
    base: undefined,
    browser: { asObject: true },
  });
})();

class Logger {
  private root: PinoLogger;

  constructor(root: PinoLogger) {
    this.root = root;
  }

  createModuleLogger(module: string) {
    const child = this.root.child({ module });
    return {
      trace: child.trace.bind(child),
      debug: child.debug.bind(child),
      info: child.info.bind(child),
      warn: child.warn.bind(child),
      error: child.error.bind(child),
      fatal: child.fatal.bind(child),
    };
  }

  // Fallback root-level logging (rarely used directly)
  debug(msg: string, meta?: any) { this.root.debug(meta || {}, msg); }
  info(msg: string, meta?: any) { this.root.info(meta || {}, msg); }
  warn(msg: string, meta?: any) { this.root.warn(meta || {}, msg); }
  error(msg: string, meta?: any) { this.root.error(meta || {}, msg); }
  fatal(msg: string, meta?: any) { this.root.fatal(meta || {}, msg); }
}

export const logger = new Logger(loggerInstance);
