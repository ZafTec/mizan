import { generateZodClientFromOpenAPI } from "openapi-zod-client";

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
		console.log("✅ Zod schemas generated successfully");
	} catch (error) {
		console.error("❌ Failed to generate Zod schemas:", error);
		process.exit(1);
	}
}

generate();
