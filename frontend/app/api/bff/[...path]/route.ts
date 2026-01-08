import { NextRequest, NextResponse } from "next/server";
import { callBackendApi, BackendApiError } from "@/lib/backend-api-client";
import { logger } from "@/lib/logger";

const bffLogger = logger.createModuleLogger("bff-proxy");

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;
    const searchParams = request.nextUrl.searchParams.toString();
    const fullPath = searchParams ? `${path}?${searchParams}` : path;

    bffLogger.info("GET request", {
      path: fullPath,
      from: request.headers.get("x-forwarded-for") ?? "unknown",
      userAgent: request.headers.get("user-agent"),
    });

    const data = await callBackendApi(fullPath);
    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      bffLogger.warn("BFF proxy error (BackendApiError)", {
        status: error.status,
        statusText: error.statusText,
      });
      return NextResponse.json(error.body, { status: error.status });
    }

    bffLogger.error("BFF proxy error (unexpected)", {
      error: error instanceof Error ? error.message : String(error),
    });

    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;

    let body = undefined;
    const contentType = request.headers.get("content-type");
    if (contentType && contentType.includes("application/json")) {
      try {
        body = await request.json();
      } catch (e) {
        bffLogger.debug("POST request with no/invalid JSON body", {
          path,
          parseError: e instanceof Error ? e.message : String(e),
        });
      }
    }

    bffLogger.info("POST request", {
      path,
      hasBody: !!body,
      from: request.headers.get("x-forwarded-for") ?? "unknown",
      userAgent: request.headers.get("user-agent"),
    });

    const data = await callBackendApi(path, {
      method: "POST",
      body,
    });

    return NextResponse.json(data, { status: 201 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      bffLogger.warn("BFF proxy error (BackendApiError)", {
        status: error.status,
        statusText: error.statusText,
      });
      return NextResponse.json(error.body, { status: error.status });
    }

    bffLogger.error("BFF proxy error (unexpected)", {
      error: error instanceof Error ? error.message : String(error),
    });

    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;
    const body = await request.json();

    bffLogger.info("PUT request", {
      path,
      from: request.headers.get("x-forwarded-for") ?? "unknown",
      userAgent: request.headers.get("user-agent"),
    });

    const data = await callBackendApi(path, {
      method: "PUT",
      body,
    });

    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      bffLogger.warn("BFF proxy error (BackendApiError)", {
        status: error.status,
        statusText: error.statusText,
      });
      return NextResponse.json(error.body, { status: error.status });
    }

    bffLogger.error("BFF proxy error (unexpected)", {
      error: error instanceof Error ? error.message : String(error),
    });

    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;

    bffLogger.info("DELETE request", {
      path,
      from: request.headers.get("x-forwarded-for") ?? "unknown",
      userAgent: request.headers.get("user-agent"),
    });

    await callBackendApi(path, { method: "DELETE" });

    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      bffLogger.warn("BFF proxy error (BackendApiError)", {
        status: error.status,
        statusText: error.statusText,
      });
      return NextResponse.json(error.body, { status: error.status });
    }

    bffLogger.error("BFF proxy error (unexpected)", {
      error: error instanceof Error ? error.message : String(error),
    });

    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function PATCH(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;
    const body = await request.json();

    bffLogger.info("PATCH request", {
      path,
      from: request.headers.get("x-forwarded-for") ?? "unknown",
      userAgent: request.headers.get("user-agent"),
    });

    const data = await callBackendApi(path, {
      method: "PATCH",
      body,
    });

    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      bffLogger.warn("BFF proxy error (BackendApiError)", {
        status: error.status,
        statusText: error.statusText,
      });
      return NextResponse.json(error.body, { status: error.status });
    }

    bffLogger.error("BFF proxy error (unexpected)", {
      error: error instanceof Error ? error.message : String(error),
    });

    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}
