"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Button, Card, Empty, Page, PageHeader, Pill, api, inputCls, postJson } from "@/components/ui";
import { can, useMe } from "@/components/useMe";
import type { ProductRow } from "@/components/views/types";

// VA Library: one row per product (= what LightGuide program / iFactory part
// numbers run as it), the version stations currently use, and what's waiting.
export default function VaLibraryPage() {
  const me = useMe();
  const [products, setProducts] = useState<ProductRow[] | null>(null);
  const [form, setForm] = useState({ name: "", partNumbers: "", description: "" });
  const [error, setError] = useState<string | null>(null);

  const load = () => api<ProductRow[]>("/api/products").then(setProducts).catch(() => setProducts([]));
  useEffect(() => {
    load();
  }, []);

  async function add() {
    setError(null);
    try {
      await postJson("/api/products", form);
      setForm({ name: "", partNumbers: "", description: "" });
      load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  return (
    <Page>
      <PageHeader eyebrow="Visual Aids" title="VA Library" />
      <p className="mb-6 max-w-3xl text-sm text-neutral-400">
        Each product has its own VA and its own step mapping - step numbers belong to that product&apos;s LightGuide program, so
        &quot;step 3&quot; on one product has nothing to do with step 3 on another. Stations only ever receive the <b>approved</b> version;
        a new upload stays a draft until an Approver signs it off.
      </p>

      {can(me, "engineer") && (
        <Card title="Add a product" className="mb-4">
          <div className="grid gap-2 md:grid-cols-[1fr_1.4fr_1.4fr_auto]">
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Product / model (e.g. CVG300-A)" className={inputCls} />
            <input value={form.partNumbers} onChange={(e) => setForm({ ...form, partNumbers: e.target.value })} placeholder="iFactory part numbers, comma separated" className={`${inputCls} font-mono`} />
            <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Description (optional)" className={inputCls} />
            <Button onClick={add} disabled={!form.name.trim()}>
              Add
            </Button>
          </div>
          {error && <p className="mt-2 text-sm text-red-400">{error}</p>}
        </Card>
      )}

      <Card>
        {!products ? (
          <Empty>Loading…</Empty>
        ) : products.length === 0 ? (
          <Empty>No products yet.{can(me, "engineer") ? " Add one above, then upload its VA." : " An Engineer adds products and uploads their VAs."}</Empty>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                <th className="pb-2">Product</th>
                <th className="pb-2">Part numbers</th>
                <th className="pb-2">Live on stations</th>
                <th className="pb-2">Waiting</th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id} className="border-t border-neutral-800">
                  <td className="py-2.5">
                    <Link href={`/va/${p.id}`} className="font-semibold text-neutral-100 hover:text-indigo-300">
                      {p.name}
                    </Link>
                    {p.description && <div className="text-xs text-neutral-500">{p.description}</div>}
                  </td>
                  <td className="py-2.5 font-mono text-xs text-neutral-400">{p.partNumbers.join(", ") || "–"}</td>
                  <td className="py-2.5">{p.publishedVersion ? <Pill value={`v${p.publishedVersion}`} /> : <span className="text-xs text-amber-400">no approved VA yet</span>}</td>
                  <td className="py-2.5 text-xs">
                    <span className="flex gap-1.5">
                      {p.pending > 0 && <Pill value="pending" />}
                      {p.drafts > 0 && <Pill value="draft" />}
                      {!p.pending && !p.drafts && <span className="text-neutral-600">–</span>}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </Page>
  );
}
