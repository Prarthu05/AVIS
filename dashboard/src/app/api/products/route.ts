import { NextResponse } from "next/server";
import { createProduct, listDocuments, listProducts } from "@/lib/vaStore";
import { getSession, requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function GET() {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const docs = listDocuments();
  return NextResponse.json(
    listProducts().map((p) => {
      const mine = docs.filter((d) => d.productId === p.id);
      const published = mine.find((d) => d.status === "approved");
      return {
        ...p,
        publishedVersion: published?.version ?? null,
        publishedDocId: published?.id ?? null,
        pending: mine.filter((d) => d.status === "pending").length,
        drafts: mine.filter((d) => d.status === "draft" || d.status === "rejected").length,
        versions: mine.length,
      };
    }),
  );
}

export async function POST(req: Request) {
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const body = (await req.json()) as { name?: string; partNumbers?: string[] | string; description?: string };
  const partNumbers = Array.isArray(body.partNumbers) ? body.partNumbers : (body.partNumbers ?? "").split(/[,\n]/);
  const product = createProduct({ name: body.name ?? "", partNumbers, description: body.description ?? "" });
  if (!product) return NextResponse.json({ error: "a product needs a unique name" }, { status: 400 });
  audit(session.name, "product.create", { product: product.name });
  return NextResponse.json(product, { status: 201 });
}
