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
    const call = (level: LogLevel, msg: string, meta?: Record<string, unknown>) => {
      // Pino signature is (obj, msg?), so put metadata first to avoid TS overload ambiguity.
      child[level](meta ?? {}, msg);
    };

    return {
      trace: (msg: string, meta?: Record<string, unknown>) => call("trace", msg, meta),
      debug: (msg: string, meta?: Record<string, unknown>) => call("debug", msg, meta),
      info: (msg: string, meta?: Record<string, unknown>) => call("info", msg, meta),
      warn: (msg: string, meta?: Record<string, unknown>) => call("warn", msg, meta),
      error: (msg: string, meta?: Record<string, unknown>) => call("error", msg, meta),
      fatal: (msg: string, meta?: Record<string, unknown>) => call("fatal", msg, meta),
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
