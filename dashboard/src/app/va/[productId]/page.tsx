"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { use, useEffect, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { Button, Card, Empty, Page, PageHeader, Pill, api, fmtTime, inputCls, postJson } from "@/components/ui";
import { can, useMe } from "@/components/useMe";
import type { Product, VaDocument } from "@/components/views/types";

export default function ProductPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const me = useMe();
  const router = useRouter();
  const [data, setData] = useState<{ product: Product; documents: VaDocument[] } | null>(null);
  const [edit, setEdit] = useState<{ name: string; partNumbers: string; description: string } | null>(null);
  const [title, setTitle] = useState("");
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = () => api<{ product: Product; documents: VaDocument[] }>(`/api/products/${productId}`).then(setData).catch((e: Error) => setError(e.message));
  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [productId]);

  async function upload() {
    const files = fileRef.current?.files;
    if (!files || files.length === 0) return;
    setUploading(true);
    setError(null);
    const form = new FormData();
    form.set("productId", productId);
    form.set("title", title);
    for (const f of Array.from(files)) form.append("file", f);
    try {
      const doc = await api<VaDocument>("/api/va", { method: "POST", body: form });
      router.push(`/va/doc/${doc.id}`);
    } catch (e) {
      setError((e as Error).message);
      setUploading(false);
    }
  }

  async function saveProduct() {
    if (!edit) return;
    try {
      await postJson(`/api/products/${productId}`, edit, "PUT");
      setEdit(null);
      load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  if (!data) return <Page>{error ? <Empty>{error}</Empty> : <Empty>Loading…</Empty>}</Page>;
  const { product, documents } = data;

  return (
    <Page>
      <PageHeader eyebrow="VA Library" title={product.name}>
        <Link href="/va" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">← all products</Link>
      </PageHeader>
      {error && <p className="mb-3 text-sm text-red-400">{error}</p>}

      <div className="mb-4 grid gap-4 lg:grid-cols-2">
        <Card title="Product" action={can(me, "engineer") && !edit && <button onClick={() => setEdit({ name: product.name, partNumbers: product.partNumbers.join(", "), description: product.description })} className="text-xs font-semibold text-indigo-400">edit</button>}>
          {edit ? (
            <div className="flex flex-col gap-2">
              <input value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} className={inputCls} placeholder="Product / model name" />
              <input value={edit.partNumbers} onChange={(e) => setEdit({ ...edit, partNumbers: e.target.value })} className={`${inputCls} font-mono`} placeholder="Part numbers, comma separated" />
              <input value={edit.description} onChange={(e) => setEdit({ ...edit, description: e.target.value })} className={inputCls} placeholder="Description" />
              <div className="flex gap-2">
                <Button onClick={saveProduct}>Save</Button>
                <Button variant="ghost" onClick={() => setEdit(null)}>Cancel</Button>
              </div>
            </div>
          ) : (
            <dl className="grid grid-cols-[8rem_1fr] gap-y-1.5 text-sm">
              <dt className="text-neutral-500">Matches</dt>
              <dd>WIP material in <span className="font-mono">{product.partNumbers.join(", ") || "(none)"}</span>, or model / LightGuide program named <span className="font-mono">{product.name}</span></dd>
              <dt className="text-neutral-500">Description</dt>
              <dd className="text-neutral-300">{product.description || "–"}</dd>
            </dl>
          )}
        </Card>
        {can(me, "engineer") && (
          <Card title="Upload a new VA version">
            <p className="mb-3 text-xs text-neutral-500">One PDF (each page becomes a page image), or several images (pages in file-name order). It becomes a draft - map steps to pages, then submit it for approval.</p>
            <div className="flex flex-col gap-2">
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Title / revision (e.g. Rev C - new bracket)" className={inputCls} />
              <input ref={fileRef} type="file" multiple accept=".pdf,.png,.jpg,.jpeg,.webp,.gif" className="text-sm text-neutral-400 file:mr-3 file:rounded-lg file:border-0 file:bg-neutral-800 file:px-3 file:py-2 file:text-neutral-200" />
              <div>
                <Button onClick={upload} disabled={uploading}>
                  <span className="flex items-center gap-1.5"><Upload size={14} /> {uploading ? "Uploading & rendering…" : "Upload"}</span>
                </Button>
              </div>
            </div>
          </Card>
        )}
      </div>

      <Card title="Versions">
        {documents.length === 0 ? (
          <Empty>No VA uploaded for this product yet.</Empty>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                <th className="pb-2">Version</th>
                <th className="pb-2">Title</th>
                <th className="pb-2">Status</th>
                <th className="pb-2">Pages / steps</th>
                <th className="pb-2">Uploaded</th>
                <th className="pb-2">Reviewed</th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => (
                <tr key={d.id} className="border-t border-neutral-800">
                  <td className="py-2.5">
                    <Link href={`/va/doc/${d.id}`} className="font-mono font-semibold text-neutral-100 hover:text-indigo-300">v{d.version}</Link>
                  </td>
                  <td className="py-2.5 text-neutral-300">{d.title}</td>
                  <td className="py-2.5"><Pill value={d.status} /></td>
                  <td className="py-2.5 font-mono text-xs text-neutral-400">{d.pages.length} pages · {d.mappings.length} steps mapped</td>
                  <td className="py-2.5 text-xs text-neutral-500">{d.uploadedBy}, {fmtTime(d.uploadedAt)}</td>
                  <td className="py-2.5 text-xs text-neutral-500">{d.reviewedBy ? `${d.reviewedBy}, ${fmtTime(d.reviewedAt)}` : "–"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </Page>
  );
}
