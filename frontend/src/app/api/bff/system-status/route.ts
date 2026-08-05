import { NextResponse } from "next/server";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";

export async function GET() {
  try {
    const data = await invokeBackend({ path: "/api/system/status" });
    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendInvokeError) {
      return NextResponse.json(
        { message: "Unable to reach the backend service." },
        { status: 502 },
      );
    }
    return NextResponse.json({ message: "Unable to reach the backend service." }, { status: 502 });
  }
}
