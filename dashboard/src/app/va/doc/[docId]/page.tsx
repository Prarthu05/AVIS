"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { use, useEffect, useMemo, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { Button, Card, Empty, Page, PageHeader, Pill, api, fmtTime, inputCls, postJson } from "@/components/ui";
import { can, useMe } from "@/components/useMe";
import type { Product, VaDocument } from "@/components/views/types";
import type { StepMapping } from "@/lib/domain";

type Doc = VaDocument & { product: Product | null };

function parsePages(text: string): number[] {
  const out: number[] = [];
  for (const part of text.split(",")) {
    const m = part.trim().match(/^(\d+)\s*(?:-\s*(\d+))?$/);
    if (!m) continue;
    const a = Number(m[1]);
    const b = m[2] ? Number(m[2]) : a;
    for (let n = Math.min(a, b); n <= Math.max(a, b) && n - Math.min(a, b) < 200; n++) out.push(n);
  }
  return out;
}

function pagesText(pages: number[]): string {
  return pages.join(", ");
}

// One VA version: page thumbnails, the (step -> pages) mapping for this
// product's LightGuide program, a "what the station shows at step N"
// preview, and the draft -> pending -> approved workflow.
export default function VaDocPage({ params }: { params: Promise<{ docId: string }> }) {
  const { docId } = use(params);
  const me = useMe();
  const router = useRouter();
  const [doc, setDoc] = useState<Doc | null>(null);
  const [rows, setRows] = useState<{ step: string; pages: string }[]>([]);
  const [defaults, setDefaults] = useState("");
  const [notes, setNotes] = useState("");
  const [previewStep, setPreviewStep] = useState("");
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);

  function apply(d: Doc) {
    setDoc(d);
    setRows(d.mappings.map((m) => ({ step: String(m.step), pages: pagesText(m.pages) })));
    setDefaults(pagesText(d.defaultPages));
    setNotes(d.notes);
    setPreviewStep((p) => p || String(d.mappings[0]?.step ?? ""));
  }

  useEffect(() => {
    api<Doc>(`/api/va/${docId}`).then(apply).catch((e: Error) => setMessage({ text: e.message, error: true }));
  }, [docId]);

  const editable = !!doc && (doc.status === "draft" || doc.status === "rejected") && can(me, "engineer");
  const mappings: StepMapping[] = useMemo(
    () => rows.filter((r) => r.step.trim() !== "" && !Number.isNaN(Number(r.step))).map((r) => ({ step: Number(r.step), pages: parsePages(r.pages) })),
    [rows],
  );
  const previewPages = useMemo(() => {
    const step = Number(previewStep);
    const mapped = mappings.filter((m) => m.step === step).flatMap((m) => m.pages);
    return mapped.length > 0 ? mapped : parsePages(defaults);
  }, [mappings, previewStep, defaults]);

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true);
    setMessage(null);
    try {
      await action();
      apply(await api<Doc>(`/api/va/${docId}`));
      setMessage({ text: success, error: false });
    } catch (e) {
      setMessage({ text: (e as Error).message, error: true });
    } finally {
      setBusy(false);
    }
  }

  const save = () => run(() => postJson(`/api/va/${docId}`, { mappings, defaultPages: parsePages(defaults), notes }, "PUT"), "Saved.");
  const submit = () => run(async () => {
    await postJson(`/api/va/${docId}`, { mappings, defaultPages: parsePages(defaults), notes }, "PUT");
    await postJson(`/api/va/${docId}/submit`, {});
  }, "Submitted - an Approver will review it.");
  const review = (approve: boolean) => run(() => postJson(`/api/va/${docId}/review`, { approve, comment }), approve ? "Approved - stations will pick it up within a minute." : "Rejected and sent back.");
  async function remove() {
    if (!confirm("Delete this draft?")) return;
    await api(`/api/va/${docId}`, { method: "DELETE" });
    router.push(`/va/${doc?.productId}`);
  }

  if (!doc) return <Page>{message ? <Empty>{message.text}</Empty> : <Empty>Loading…</Empty>}</Page>;
  const fileUrl = (file: string) => `/api/va/${doc.id}/file/${file}`;
  const pageFile = (n: number) => doc.pages.find((p) => p.n === n)?.file;

  return (
    <Page>
      <PageHeader eyebrow={`VA · ${doc.product?.name ?? ""}`} title={`v${doc.version} - ${doc.title}`}>
        <Pill value={doc.status} />
        <Link href={`/va/${doc.productId}`} className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">← {doc.product?.name}</Link>
      </PageHeader>
      {message && <p className={`mb-3 text-sm ${message.error ? "text-red-400" : "text-emerald-400"}`}>{message.text}</p>}
      {doc.status === "rejected" && doc.reviewComment && (
        <p className="mb-3 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-300">
          Rejected by {doc.reviewedBy}: {doc.reviewComment}
        </p>
      )}
      {doc.pdfOnly && <p className="mb-3 text-xs text-amber-400">The PDF couldn&apos;t be split into page images on the server (pdftoppm missing) - stations open the PDF at the mapped page instead.</p>}

      <div className="grid gap-4 xl:grid-cols-[1fr_26rem]">
        <div className="flex flex-col gap-4">
          <Card title={`Step → page mapping (${doc.product?.name ?? "product"}'s LightGuide steps)`}>
            <p className="mb-3 text-xs text-neutral-500">
              Pages like <span className="font-mono">2</span>, <span className="font-mono">2, 3</span> or <span className="font-mono">4-6</span>. A step can show several pages; add the same step twice if that&apos;s easier.
            </p>
            <div className="flex flex-col gap-1.5">
              <div className="grid grid-cols-[6rem_1fr_2rem] gap-2 font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                <span>Step</span>
                <span>Pages</span>
                <span />
              </div>
              {rows.map((r, i) => (
                <div key={i} className="grid grid-cols-[6rem_1fr_2rem] items-center gap-2">
                  <input disabled={!editable} value={r.step} onChange={(e) => setRows(rows.map((x, j) => (j === i ? { ...x, step: e.target.value.replace(/\D/g, "") } : x)))} className={`${inputCls} font-mono`} />
                  <input disabled={!editable} value={r.pages} onChange={(e) => setRows(rows.map((x, j) => (j === i ? { ...x, pages: e.target.value } : x)))} onFocus={() => setPreviewStep(r.step)} className={`${inputCls} font-mono`} />
                  {editable && (
                    <button onClick={() => setRows(rows.filter((_, j) => j !== i))} className="text-neutral-600 hover:text-red-400" title="Remove row">
                      <Trash2 size={15} />
                    </button>
                  )}
                </div>
              ))}
              {rows.length === 0 && <Empty>No steps mapped yet.</Empty>}
              {editable && (
                <button
                  onClick={() => setRows([...rows, { step: String(Math.max(0, ...rows.map((r) => Number(r.step) || 0)) + 1), pages: "" }])}
                  className="mt-1 flex w-fit items-center gap-1 text-sm font-semibold text-indigo-400 hover:text-indigo-300"
                >
                  <Plus size={14} /> Add step
                </button>
              )}
            </div>
            <div className="mt-4 grid gap-2 md:grid-cols-2">
              <label className="text-xs text-neutral-500">
                Default pages (steps with no row - e.g. an overview; empty = keep the last page)
                <input disabled={!editable} value={defaults} onChange={(e) => setDefaults(e.target.value)} className={`${inputCls} mt-1 w-full font-mono`} />
              </label>
              <label className="text-xs text-neutral-500">
                Notes for the approver
                <input disabled={!editable} value={notes} onChange={(e) => setNotes(e.target.value)} className={`${inputCls} mt-1 w-full`} />
              </label>
            </div>
            {editable && (
              <div className="mt-4 flex flex-wrap gap-2">
                <Button onClick={save} disabled={busy} variant="ghost">Save draft</Button>
                <Button onClick={submit} disabled={busy}>Submit for approval</Button>
                <Button onClick={remove} disabled={busy} variant="danger">Delete draft</Button>
              </div>
            )}
          </Card>

          {doc.status === "pending" && (
            <Card title="Approval">
              <p className="mb-2 text-sm text-neutral-400">
                Submitted by {doc.submittedBy} {fmtTime(doc.submittedAt)}. {doc.notes && <span className="text-neutral-300">“{doc.notes}”</span>}
              </p>
              {can(me, "approver") ? (
                <div className="flex flex-col gap-2">
                  <input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Comment (required to reject)" className={inputCls} />
                  <div className="flex gap-2">
                    <Button onClick={() => review(true)} disabled={busy}>Approve - send to stations</Button>
                    <Button onClick={() => review(false)} disabled={busy} variant="danger">Reject</Button>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-amber-400">Waiting for an Approver.</p>
              )}
            </Card>
          )}
          {(doc.status === "approved" || doc.status === "superseded") && (
            <p className="text-sm text-neutral-400">
              {doc.status === "approved" ? "Live on all stations." : "Replaced by a newer approved version."} Approved by {doc.reviewedBy} {fmtTime(doc.reviewedAt)}
              {doc.reviewComment && ` - “${doc.reviewComment}”`}. To change it, upload a new version.
            </p>
          )}

          <Card title={`All pages (${doc.pages.length})`}>
            {doc.pdfOnly ? (
              <a href={fileUrl("original.pdf")} target="_blank" rel="noreferrer" className="text-sm text-indigo-400">Open the PDF ({doc.pages.length} pages)</a>
            ) : (
              <div className="grid grid-cols-3 gap-3 md:grid-cols-4 lg:grid-cols-6">
                {doc.pages.map((p) => {
                  const steps = mappings.filter((m) => m.pages.includes(p.n)).map((m) => m.step);
                  return (
                    <figure key={p.n} className={`overflow-hidden rounded-lg border ${previewPages.includes(p.n) ? "border-indigo-400" : "border-neutral-800"} bg-neutral-950`}>
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img src={fileUrl(p.file)} alt={`Page ${p.n}`} className="aspect-[3/4] w-full object-contain" loading="lazy" />
                      <figcaption className="flex justify-between px-2 py-1 font-mono text-[10.5px] text-neutral-400">
                        <span>p{p.n}</span>
                        <span className="text-indigo-300">{steps.length ? `step ${steps.join(", ")}` : ""}</span>
                      </figcaption>
                    </figure>
                  );
                })}
              </div>
            )}
          </Card>
        </div>

        <Card title="Station preview" className="h-fit xl:sticky xl:top-4">
          <label className="mb-3 flex items-center gap-2 text-xs text-neutral-500">
            LightGuide step
            <input value={previewStep} onChange={(e) => setPreviewStep(e.target.value.replace(/\D/g, ""))} className={`${inputCls} w-20 font-mono`} />
          </label>
          {previewPages.length === 0 ? (
            <Empty>Nothing mapped - the station keeps showing the previous page.</Empty>
          ) : (
            <div className="flex max-h-[75vh] flex-col gap-2 overflow-y-auto">
              {previewPages.map((n) => {
                const file = pageFile(n);
                if (!file) return <p key={n} className="text-xs text-red-400">Page {n} doesn&apos;t exist in this document.</p>;
                return doc.pdfOnly ? (
                  <p key={n} className="text-xs text-neutral-400">PDF page {n}</p>
                ) : (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img key={n} src={fileUrl(file)} alt={`Page ${n}`} className="w-full rounded border border-neutral-800 bg-white" />
                );
              })}
            </div>
          )}
        </Card>
      </div>
    </Page>
  );
}
