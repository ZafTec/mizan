export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { validateStartupConfig } = await import("./lib/startup-validation");

    try {
      validateStartupConfig();
    } catch (error) {
      if (process.env.NEXT_BUILD) {
        console.warn("Skipping startup validation during build");
      } else {
        throw error;
      }
    }

    // Initialize OpenTelemetry (server-side only, skip during build)
    if (!process.env.NEXT_BUILD) {
      const tracingEndpoint = process.env.OTLP_ENDPOINT_URL;
      const lokiEndpoint = process.env.LOKI_OTLP_ENDPOINT;

      if (tracingEndpoint || lokiEndpoint) {
        try {
          const { NodeSDK } = await import("@opentelemetry/sdk-node");
          const { getNodeAutoInstrumentations } =
            await import("@opentelemetry/auto-instrumentations-node");
          const { OTLPTraceExporter } =
            await import("@opentelemetry/exporter-trace-otlp-grpc");
          const { PrometheusExporter } =
            await import("@opentelemetry/exporter-prometheus");
          const { OTLPLogExporter } =
            await import("@opentelemetry/exporter-logs-otlp-http");
          const { SimpleLogRecordProcessor } =
            await import("@opentelemetry/sdk-logs");
          const { resourceFromAttributes } =
            await import("@opentelemetry/resources");
          const { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } =
            await import("@opentelemetry/semantic-conventions");

          const resource = resourceFromAttributes({
            [ATTR_SERVICE_NAME]: "Mizan.Frontend",
            [ATTR_SERVICE_VERSION]: "1.2.0",
          });

          const sdkConfig: NonNullable<
            ConstructorParameters<typeof NodeSDK>[0]
          > = {
            resource,
            instrumentations: [
              getNodeAutoInstrumentations({
                "@opentelemetry/instrumentation-fs": { enabled: false },
              }),
            ],
          };

          if (tracingEndpoint) {
            sdkConfig.traceExporter = new OTLPTraceExporter({
              url: tracingEndpoint,
            });
          }

          if (lokiEndpoint) {
            sdkConfig.logRecordProcessor = new SimpleLogRecordProcessor(
              new OTLPLogExporter({ url: lokiEndpoint }),
            );
          }

          const metricsPort = parseInt(
            process.env.OTEL_METRICS_PORT || "9464",
            10,
          );
          sdkConfig.metricReader = new PrometheusExporter({
            port: metricsPort,
          });

          const sdk = new NodeSDK(sdkConfig);
          sdk.start();

          process.on("SIGTERM", () => sdk.shutdown());

          console.log(
            `[OTel] Mizan.Frontend telemetry initialized (traces: ${!!tracingEndpoint}, logs: ${!!lokiEndpoint}, metrics: :${metricsPort})`,
          );
        } catch (error) {
          console.warn("[OTel] Failed to initialize telemetry:", error);
        }
      }
    }
  }
}
