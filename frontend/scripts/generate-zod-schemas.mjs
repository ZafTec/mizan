import { generateZodClientFromOpenAPI } from "openapi-zod-client";
import { logger } from "@/lib/logger";

const zodLogger = logger.createModuleLogger("zod-schema-generator");

const OPENAPI_URL = process.env.API_URL || "http://localhost:5000";

async function generate() {
	try {
		await generateZodClientFromOpenAPI({
			openApiDoc: `${OPENAPI_URL}/swagger/v1/swagger.json`,
			distPath: "./lib/validations/api.generated.ts",
			options: {
				shouldExportAllTypes: true,
				withAlias: true,
				baseUrl: OPENAPI_URL,
			},
		});
		zodLogger.info("✅ Successfully generated Zod schemas from OpenAPI spec");
	} catch (error) {
		zodLogger.error("❌ Failed to generate Zod schemas from OpenAPI spec", { error });
		process.exit(1);
	}
}

generate();
