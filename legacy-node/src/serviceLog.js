const LEVEL_RANK = {
  error: 0,
  warn: 1,
  info: 2,
  debug: 3
};

export function resolveLogLevel(env = process.env) {
  const raw = (env.LOG_LEVEL || "warn").trim().toLowerCase();
  return Object.hasOwn(LEVEL_RANK, raw) ? raw : "warn";
}

export function shouldLog(level, env = process.env) {
  const configured = resolveLogLevel(env);
  return LEVEL_RANK[level] <= LEVEL_RANK[configured];
}

export function logAt(level, log, line, env = process.env) {
  if (!shouldLog(level, env)) {
    return;
  }
  const fn = log?.[level];
  if (typeof fn === "function") {
    fn(line);
    return;
  }
  if (level === "error" && typeof log?.error === "function") {
    log.error(line);
    return;
  }
  if (typeof log?.warn === "function") {
    log.warn(line);
  }
}

export function logWarn(log, line, env = process.env) {
  logAt("warn", log, line, env);
}

export function logStartupFromIssues(
  config,
  issues,
  log = console,
  env = process.env
) {
  if (issues.length > 0) {
    logWarn(
      log,
      `[startup] config issues: ${JSON.stringify(issues)}`,
      env
    );
  }

  if (shouldLog("info", env)) {
    logAt("info", log, `[startup] config ${JSON.stringify(config)}`, env);
  }
}

export function logListenMessage(log, line, env = process.env) {
  logAt("info", log, line, env);
}
