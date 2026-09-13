import { faro, getWebInstrumentations, initializeFaro } from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";

const faroUrl = process.env.NEXT_PUBLIC_FARO_URL;

if (faroUrl) {
  try {
    initializeFaro({
      url: faroUrl,
      app: {
        name: process.env.NEXT_PUBLIC_FARO_APP_NAME || "mizan-frontend",
        version: process.env.NEXT_PUBLIC_APP_VERSION,
        environment: process.env.NODE_ENV,
      },
      instrumentations: [
        ...getWebInstrumentations(),
        new TracingInstrumentation({
          instrumentationOptions: {
            propagateTraceHeaderCorsUrls: [/^https:\/\/mizan\.zaftech\.co\//],
          },
        }),
      ],
    });
  } catch {
    // Telemetry must never break the app.
  }
}

export function onRouterTransitionStart(url: string) {
  faro.api?.setView({ name: url });
}
