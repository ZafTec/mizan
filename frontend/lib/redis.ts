import "server-only";
import { logger } from "@/lib/logger";

const redisLogger = logger.createModuleLogger("redis");

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let client: any | null = null;

// Dynamic import so `bun` is never evaluated during Next.js build workers
// (which run under Node.js). The import only fires on the first real request.
async function getClient() {
  if (!client) {
    const { RedisClient } = await import("bun");
    client = new RedisClient(process.env.REDIS_URL!);
    client.onconnect = () => redisLogger.info("Redis connected");
    client.onclose = (error: unknown) => {
      if (error) redisLogger.error("Redis disconnected", { error: String(error) });
    };
  }
  return client;
}

export function getBetterAuthSecondaryStorage() {
  return {
    get: async (key: string): Promise<string | null> => {
      return (await getClient()).get(key);
    },
    set: async (key: string, value: string, ttl?: number): Promise<void> => {
      const c = await getClient();
      if (ttl) {
        await c.send("SET", [key, value, "EX", String(ttl)]);
      } else {
        await c.set(key, value);
      }
    },
    delete: async (key: string): Promise<void> => {
      await (await getClient()).del(key);
    },
  };
}
