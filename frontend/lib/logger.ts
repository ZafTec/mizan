type LogLevel = "fatal" | "error" | "warn" | "info" | "debug" | "trace";

const isProd = process.env.NODE_ENV === "production";
const level: LogLevel = (process.env.LOG_LEVEL as LogLevel) || (isProd ? "info" : "debug");

const levelPriority: Record<LogLevel, number> = {
  trace: 0,
  debug: 1,
  info: 2,
  warn: 3,
  error: 4,
  fatal: 5,
};

const redactionKeys = [
  "password",
  "token",
  "secret",
  "authorization",
  "cookie",
  "apikey",
  "api_key",
  "bearer",
];

function redact(obj: Record<string, unknown>): Record<string, unknown> {
  const redacted: Record<string, unknown> = {};
  for (const key in obj) {
    if (redactionKeys.some((p) => key.toLowerCase().includes(p))) {
      redacted[key] = "[REDACTED]";
    } else if (typeof obj[key] === "object" && obj[key] !== null) {
      redacted[key] = redact(obj[key] as Record<string, unknown>);
    } else {
      redacted[key] = obj[key];
    }
  }
  return redacted;
}

function emit(
  lvl: LogLevel,
  msg: string,
  meta?: Record<string, unknown>,
  moduleName?: string,
) {
  const timestamp = new Date().toISOString();

  if (isProd) {
    const payload = JSON.stringify({
      timestamp,
      level: lvl,
      module: moduleName,
      msg,
      ...redact(meta || {}),
    });
    if (lvl === "error" || lvl === "fatal") console.error(payload);
    else if (lvl === "warn") console.warn(payload);
    else console.log(payload);
    return;
  }

  const levelStr = lvl.toUpperCase().padStart(5);
  const moduleStr = moduleName ? `[${moduleName}] ` : "";
  const metaStr =
    meta && Object.keys(meta).length > 0
      ? " " + JSON.stringify(redact(meta), null, 0)
      : "";
  const formatted = `[${timestamp}] ${levelStr} ${moduleStr}${msg}${metaStr}`;

  if (lvl === "error" || lvl === "fatal") console.error(formatted);
  else if (lvl === "warn") console.warn(formatted);
  else if (lvl === "debug" || lvl === "trace") console.debug(formatted);
  else console.info(formatted);
}

function shouldLog(lvl: LogLevel): boolean {
  return levelPriority[lvl] >= levelPriority[level];
}

type LogFn = (msg: string, meta?: Record<string, unknown>) => void;

interface ModuleLogger {
  trace: LogFn;
  debug: LogFn;
  info: LogFn;
  warn: LogFn;
  error: LogFn;
  fatal: LogFn;
}

function makeLogFn(lvl: LogLevel, moduleName?: string): LogFn {
  return (msg, meta) => {
    if (shouldLog(lvl)) emit(lvl, msg, meta, moduleName);
  };
}

function createModuleLogger(moduleName: string): ModuleLogger {
  return {
    trace: makeLogFn("trace", moduleName),
    debug: makeLogFn("debug", moduleName),
    info: makeLogFn("info", moduleName),
    warn: makeLogFn("warn", moduleName),
    error: makeLogFn("error", moduleName),
    fatal: makeLogFn("fatal", moduleName),
  };
}

export const logger = {
  trace: makeLogFn("trace"),
  debug: makeLogFn("debug"),
  info: makeLogFn("info"),
  warn: makeLogFn("warn"),
  error: makeLogFn("error"),
  fatal: makeLogFn("fatal"),
  createModuleLogger,
};
