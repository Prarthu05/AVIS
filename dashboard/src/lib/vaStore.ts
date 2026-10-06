import fs from "fs";
import path from "path";
import { createHash, randomUUID } from "crypto";
import { execFile } from "child_process";
import { promisify } from "util";
import type { Product, StepMapping, VaDocument } from "./domain";
import { dataPath, readJson, writeJson } from "./jsonStore";

const execFileAsync = promisify(execFile);
const PRODUCTS = "products.json";
const DOCS = "va-documents.json";
const FILES_DIR = "va-files";

// ---------- Products ----------
export function listProducts(): Product[] {
  return readJson<Product[]>(PRODUCTS, []).sort((a, b) => a.name.localeCompare(b.name));
}

export function getProduct(id: string): Product | undefined {
  return listProducts().find((p) => p.id === id);
}

export function createProduct(input: { name: string; partNumbers: string[]; description: string }): Product | null {
  const list = listProducts();
  const name = input.name.trim();
  if (!name || list.some((p) => p.name.toLowerCase() === name.toLowerCase())) return null;
  const product: Product = {
    id: randomUUID(),
    name,
    partNumbers: cleanPartNumbers(input.partNumbers),
    description: input.description.trim(),
    createdAt: new Date().toISOString(),
  };
  list.push(product);
  writeJson(PRODUCTS, list);
  return product;
}

export function updateProduct(id: string, input: { name?: string; partNumbers?: string[]; description?: string }): Product | null {
  const list = listProducts();
  const idx = list.findIndex((p) => p.id === id);
  if (idx < 0) return null;
  if (input.name !== undefined) {
    const name = input.name.trim();
    if (!name || list.some((p) => p.id !== id && p.name.toLowerCase() === name.toLowerCase())) return null;
    list[idx].name = name;
  }
  if (input.partNumbers !== undefined) list[idx].partNumbers = cleanPartNumbers(input.partNumbers);
  if (input.description !== undefined) list[idx].description = input.description.trim();
  writeJson(PRODUCTS, list);
  return list[idx];
}

function cleanPartNumbers(values: string[]): string[] {
  return Array.from(new Set(values.map((v) => v.trim()).filter(Boolean)));
}

// ---------- VA documents ----------
export function listDocuments(): VaDocument[] {
  return readJson<VaDocument[]>(DOCS, []);
}

export function getDocument(id: string): VaDocument | undefined {
  return listDocuments().find((d) => d.id === id);
}

function saveDocument(doc: VaDocument): VaDocument {
  const list = listDocuments();
  const idx = list.findIndex((d) => d.id === doc.id);
  if (idx < 0) list.push(doc);
  else list[idx] = doc;
  writeJson(DOCS, list);
  return doc;
}

export function documentDir(docId: string): string {
  return dataPath(FILES_DIR, docId);
}

/** The approved version a station uses for this product (null = none approved yet). */
export function publishedDocument(productId: string): VaDocument | null {
  return listDocuments().find((d) => d.productId === productId && d.status === "approved") ?? null;
}

const SAFE_FILE = /^[A-Za-z0-9._-]+$/;

/** Absolute path of a stored VA file, or null for anything outside the document's own folder. */
export function documentFilePath(docId: string, file: string): string | null {
  if (!SAFE_FILE.test(docId) || !SAFE_FILE.test(file)) return null;
  const full = path.join(documentDir(docId), file);
  return fs.existsSync(full) ? full : null;
}

const IMAGE_EXT = new Set([".png", ".jpg", ".jpeg", ".webp", ".gif"]);

export interface UploadFile {
  name: string;
  data: Buffer;
}

/**
 * New draft version of a product's VA. A single PDF is rendered to one PNG
 * per page with poppler's pdftoppm (installed on the Pi); if that isn't
 * available the PDF is kept as-is and stations open it at the mapped page.
 * Several images become pages in file-name order.
 */
export async function createDraft(productId: string, files: UploadFile[], title: string, uploadedBy: string): Promise<VaDocument> {
  if (!getProduct(productId)) throw new Error("Unknown product");
  if (files.length === 0) throw new Error("No file uploaded");

  const id = randomUUID();
  const dir = documentDir(id);
  fs.mkdirSync(dir, { recursive: true });

  const pdfs = files.filter((f) => path.extname(f.name).toLowerCase() === ".pdf");
  const images = files.filter((f) => IMAGE_EXT.has(path.extname(f.name).toLowerCase()));
  if (pdfs.length > 1 || (pdfs.length === 1 && images.length > 0)) throw new Error("Upload one PDF, or one or more images - not both");
  if (pdfs.length === 0 && images.length === 0) throw new Error("Only PDF, PNG, JPG, WEBP or GIF files are accepted");

  let doc: VaDocument;
  const base = {
    id,
    productId,
    version: nextVersion(productId),
    title: title.trim() || files[0].name,
    mappings: [] as StepMapping[],
    defaultPages: [] as number[],
    status: "draft" as const,
    notes: "",
    uploadedBy,
    uploadedAt: new Date().toISOString(),
  };

  try {
    if (pdfs.length === 1) {
      const original = "original.pdf";
      fs.writeFileSync(path.join(dir, original), pdfs[0].data);
      const rendered = await renderPdf(path.join(dir, original), dir);
      doc = {
        ...base,
        originalFile: pdfs[0].name,
        kind: "pdf",
        pdfOnly: rendered.length === 0,
        pages: rendered.length > 0 ? rendered : countPdfPages(pdfs[0].data).map((n) => ({ n, file: original })),
      };
    } else {
      const sorted = [...images].sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
      const pages = sorted.map((img, i) => {
        const file = `page-${String(i + 1).padStart(3, "0")}${path.extname(img.name).toLowerCase()}`;
        fs.writeFileSync(path.join(dir, file), img.data);
        return { n: i + 1, file };
      });
      doc = { ...base, originalFile: sorted.map((s) => s.name).join(", "), kind: "images", pdfOnly: false, pages };
    }
  } catch (e) {
    fs.rmSync(dir, { recursive: true, force: true });
    throw e;
  }

  if (doc.pages.length === 0) {
    fs.rmSync(dir, { recursive: true, force: true });
    throw new Error("The file has no pages");
  }
  return saveDocument(doc);
}

function nextVersion(productId: string): number {
  return Math.max(0, ...listDocuments().filter((d) => d.productId === productId).map((d) => d.version)) + 1;
}

async function renderPdf(pdfPath: string, outDir: string) {
  try {
    await execFileAsync("pdftoppm", ["-png", "-r", "110", pdfPath, path.join(outDir, "page")], { timeout: 120_000 });
  } catch {
    return [];
  }
  // pdftoppm names pages page-1.png / page-01.png ... depending on page count.
  return fs
    .readdirSync(outDir)
    .filter((f) => /^page-\d+\.png$/.test(f))
    .map((f) => ({ n: Number(f.match(/(\d+)/)![1]), file: f }))
    .sort((a, b) => a.n - b.n);
}

/** Rough page count straight from the PDF objects - only used when pdftoppm isn't installed. */
function countPdfPages(data: Buffer): number[] {
  const matches = data.toString("latin1").match(/\/Type\s*\/Page(?!s)/g);
  const count = Math.max(1, matches?.length ?? 1);
  return Array.from({ length: count }, (_, i) => i + 1);
}

/** Mapping edits are only allowed while a version is a draft (or rejected and being fixed). */
export function updateDraft(
  id: string,
  input: { title?: string; notes?: string; mappings?: StepMapping[]; defaultPages?: number[] },
): VaDocument | { error: string } {
  const doc = getDocument(id);
  if (!doc) return { error: "not found" };
  if (doc.status !== "draft" && doc.status !== "rejected") return { error: `a ${doc.status} version can't be edited - upload a new version instead` };
  const pageNumbers = new Set(doc.pages.map((p) => p.n));
  const validPages = (pages: number[]) => Array.from(new Set(pages.filter((n) => pageNumbers.has(n)))).sort((a, b) => a - b);

  if (input.mappings !== undefined) {
    const mappings = input.mappings
      .filter((m) => Number.isInteger(m.step) && m.step >= 0)
      .map((m) => ({ step: m.step, pages: validPages(m.pages ?? []) }))
      .filter((m) => m.pages.length > 0)
      .sort((a, b) => a.step - b.step);
    doc.mappings = mappings;
  }
  if (input.defaultPages !== undefined) doc.defaultPages = validPages(input.defaultPages);
  if (input.title !== undefined) doc.title = input.title.trim() || doc.title;
  if (input.notes !== undefined) doc.notes = input.notes.slice(0, 2000);
  if (doc.status === "rejected") doc.status = "draft";
  return saveDocument(doc);
}

export function submitForApproval(id: string, by: string): VaDocument | { error: string } {
  const doc = getDocument(id);
  if (!doc) return { error: "not found" };
  if (doc.status !== "draft" && doc.status !== "rejected") return { error: `only a draft can be submitted (this one is ${doc.status})` };
  if (doc.mappings.length === 0 && doc.defaultPages.length === 0) return { error: "map at least one step (or set default pages) before submitting" };
  doc.status = "pending";
  doc.submittedBy = by;
  doc.submittedAt = new Date().toISOString();
  return saveDocument(doc);
}

/**
 * Approver decision. Approving makes this version the one every station
 * downloads; the previously approved version becomes "superseded" (kept for
 * traceability, never deleted).
 */
export function reviewDocument(id: string, approve: boolean, by: string, comment: string): VaDocument | { error: string } {
  const list = listDocuments();
  const doc = list.find((d) => d.id === id);
  if (!doc) return { error: "not found" };
  if (doc.status !== "pending") return { error: `only a pending version can be reviewed (this one is ${doc.status})` };
  if (approve) {
    for (const other of list) {
      if (other.productId === doc.productId && other.status === "approved") other.status = "superseded";
    }
  }
  doc.status = approve ? "approved" : "rejected";
  doc.reviewedBy = by;
  doc.reviewedAt = new Date().toISOString();
  doc.reviewComment = comment.slice(0, 1000);
  writeJson(DOCS, list);
  return doc;
}

export function deleteDraft(id: string): boolean | { error: string } {
  const doc = getDocument(id);
  if (!doc) return false;
  if (doc.status !== "draft" && doc.status !== "rejected") return { error: "only drafts can be deleted - approved history is kept" };
  writeJson(DOCS, listDocuments().filter((d) => d.id !== id));
  fs.rmSync(documentDir(id), { recursive: true, force: true });
  return true;
}

// ---------- What stations download ----------
export interface StationVaMap {
  version: string;
  generatedAt: string;
  products: {
    productId: string;
    product: string;
    partNumbers: string[];
    docId: string;
    docVersion: number;
    title: string;
    pdfOnly: boolean;
    pages: { n: number; file: string; url: string }[];
    steps: { step: number; pages: number[] }[];
    defaultPages: number[];
  }[];
}

/**
 * Approved VA for every product, keyed the way the middleware looks it up:
 * (product, step) -> pages. `version` is a content hash so stations can skip
 * re-downloading when nothing changed.
 */
export function buildStationVaMap(): StationVaMap {
  const products = listProducts()
    .map((p) => {
      const doc = publishedDocument(p.id);
      if (!doc) return null;
      return {
        productId: p.id,
        product: p.name,
        partNumbers: p.partNumbers,
        docId: doc.id,
        docVersion: doc.version,
        title: doc.title,
        pdfOnly: doc.pdfOnly,
        pages: doc.pages.map((pg) => ({ ...pg, url: `/api/station/va-file/${doc.id}/${pg.file}` })),
        steps: doc.mappings,
        defaultPages: doc.defaultPages,
      };
    })
    .filter((p): p is NonNullable<typeof p> => p !== null);
  const version = createHash("sha256").update(JSON.stringify(products)).digest("hex").slice(0, 16);
  return { version, generatedAt: new Date().toISOString(), products };
}
