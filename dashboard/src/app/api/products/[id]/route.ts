import { NextResponse } from "next/server";
import { getProduct, listDocuments, updateProduct } from "@/lib/vaStore";
import { getSession, requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function GET(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const product = getProduct(id);
  if (!product) return NextResponse.json({ error: "not found" }, { status: 404 });
  const documents = listDocuments()
    .filter((d) => d.productId === id)
    .sort((a, b) => b.version - a.version);
  return NextResponse.json({ product, documents });
}

export async function PUT(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const body = (await req.json()) as { name?: string; partNumbers?: string[] | string; description?: string };
  const partNumbers = body.partNumbers === undefined ? undefined : Array.isArray(body.partNumbers) ? body.partNumbers : body.partNumbers.split(/[,\n]/);
  const product = updateProduct(id, { name: body.name, partNumbers, description: body.description });
  if (!product) return NextResponse.json({ error: "not found, or that name is already used" }, { status: 400 });
  audit(session.name, "product.update", { product: product.name });
  return NextResponse.json(product);
}
