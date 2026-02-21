import "server-only";
import { RedisClient } from "bun";
import { logger } from "@/lib/logger";

const redisLogger = logger.createModuleLogger("redis");

let client: RedisClient | null = null;

function getClient(): RedisClient {
  if (!client) {
    // RedisClient reads REDIS_URL env var by default, added this so that it throws an error if not provided
    client = new RedisClient(process.env.REDIS_URL!);
    client.onconnect = () => redisLogger.info("Redis connected");
    client.onclose = (error) => {
      if (error) redisLogger.error("Redis disconnected", { error: String(error) });
    };
  }
  return client;
}

export function getBetterAuthSecondaryStorage() {
  return {
    get: async (key: string): Promise<string | null> => {
      return getClient().get(key);
    },
    set: async (key: string, value: string, ttl?: number): Promise<void> => {
      if (ttl) {
        // Atomic SET with EX — avoids race condition between set + expire
        await getClient().send("SET", [key, value, "EX", String(ttl)]);
      } else {
        await getClient().set(key, value);
      }
    },
    delete: async (key: string): Promise<void> => {
      await getClient().del(key);
    },
  };
}
