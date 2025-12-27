import { NextRequest, NextResponse } from "next/server";
import { callBackendApi, BackendApiError } from "@/lib/backend-api-client";

/**
 * BFF Proxy Route - Forwards authenticated requests to backend
 *
 * Example:
 * - Frontend: GET /api/bff/recipes
 * - Proxies to: GET http://backend:8080/api/recipes
 * - With: X-BFF-Secret, X-User-Id headers
 */

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  try {
    const { path: pathSegments } = await params;
    const path = `/api/${pathSegments.join("/")}`;
    const searchParams = request.nextUrl.searchParams.toString();
    const fullPath = searchParams ? `${path}?${searchParams}` : path;

    const data = await callBackendApi(fullPath);
    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
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

    // Some POST requests (like toggle favorite) might not have a body
    let body = undefined;
    const contentType = request.headers.get("content-type");
    if (contentType && contentType.includes("application/json")) {
      try {
        body = await request.json();
      } catch (e) {
        // Body is empty or not valid JSON, ignore
      }
    }

    const data = await callBackendApi(path, {
      method: "POST",
      body,
    });

    return NextResponse.json(data, { status: 201 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
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

    const data = await callBackendApi(path, {
      method: "PUT",
      body,
    });

    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
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

    await callBackendApi(path, { method: "DELETE" });

    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
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

    const data = await callBackendApi(path, {
      method: "PATCH",
      body,
    });

    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}
