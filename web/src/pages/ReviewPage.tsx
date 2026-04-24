import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { api } from '../api';
import type { DeliveryNoteDetail, UpdateDeliveryNoteDto } from '../types';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

function ConfBadge({ value }: { value: number | null | undefined }) {
  if (value == null) return <span className="conf">—</span>;
  const pct = Math.round(value * 100);
  const tier = pct >= 85 ? 'high' : pct >= 60 ? 'med' : 'low';
  return <span className={`conf ${tier}`}>{pct}%</span>;
}

export default function ReviewPage() {
  const { id } = useParams();
  const nav = useNavigate();
  const [note, setNote] = useState<DeliveryNoteDetail | null>(null);
  const [form, setForm] = useState<UpdateDeliveryNoteDto | null>(null);
  const [pageCount, setPageCount] = useState<number>(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.getNote(id).then((n) => {
      setNote(n);
      setForm({
        deliveryNoteNo: n.deliveryNoteNo,
        projectNumber: n.projectNumber,
        deliveryDate: n.deliveryDate,
        assigneeCompanyId: n.assigneeCompanyId,
        assigneeRawText: n.assigneeRawText,
        supplierName: n.supplierName,
        site: n.site,
        costCentre: n.costCentre
      });
    }).catch((e) => setError(String(e)));
  }, [id]);

  const pdfSrc = useMemo(() => (id ? api.pdfUrl(id) : ''), [id]);

  if (!note || !form) {
    return <div className="muted">{error ? <span className="banner error">{error}</span> : 'Loading…'}</div>;
  }

  const update = <K extends keyof UpdateDeliveryNoteDto>(k: K, v: UpdateDeliveryNoteDto[K]) =>
    setForm((prev) => (prev ? { ...prev, [k]: v } : prev));

  const save = async () => {
    if (!id) return;
    setBusy(true);
    setError(null);
    try {
      const updated = await api.updateNote(id, form);
      setNote(updated);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    if (!id) return;
    setBusy(true);
    setError(null);
    try {
      await api.updateNote(id, form);
      await api.confirmNote(id);
      nav('/notes');
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const retry = async () => {
    if (!id) return;
    setBusy(true);
    setError(null);
    try {
      const refreshed = await api.retryExtraction(id);
      setNote(refreshed);
      setForm({
        deliveryNoteNo: refreshed.deliveryNoteNo,
        projectNumber: refreshed.projectNumber,
        deliveryDate: refreshed.deliveryDate,
        assigneeCompanyId: refreshed.assigneeCompanyId,
        assigneeRawText: refreshed.assigneeRawText,
        supplierName: refreshed.supplierName,
        site: refreshed.site,
        costCentre: refreshed.costCentre
      });
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const c = note.fieldConfidences ?? {};

  return (
    <div>
      <div className="flex space-between" style={{ marginBottom: 12 }}>
        <div>
          <h2 style={{ margin: 0 }}>{note.originalFileName}</h2>
          <div className="muted" style={{ fontSize: 12 }}>
            <span className={`status-pill status-${note.status}`}>{note.status}</span>
            {note.modelIdUsed && <> · model <code>{note.modelIdUsed}</code></>}
            {note.confirmedAt && <> · confirmed by {note.confirmedBy} at {new Date(note.confirmedAt).toLocaleString()}</>}
          </div>
        </div>
        <div className="flex">
          <button className="btn secondary" onClick={() => nav('/notes')}>Back</button>
          {note.status === 'ExtractionFailed' && (
            <button className="btn" onClick={retry} disabled={busy}>Retry extraction</button>
          )}
        </div>
      </div>

      {error && <div className="banner error">{error}</div>}
      {note.extractionError && <div className="banner error">Extraction error: {note.extractionError}</div>}

      <div className="review-layout">
        <div className="pdf-pane">
          <Document
            file={pdfSrc}
            onLoadSuccess={({ numPages }) => setPageCount(numPages)}
            onLoadError={(err) => setError(`PDF load failed: ${err.message}`)}
            loading={<div className="muted">Loading PDF…</div>}
          >
            {Array.from({ length: pageCount }, (_, i) => (
              <Page
                key={i}
                pageNumber={i + 1}
                width={640}
                renderTextLayer
                renderAnnotationLayer={false}
              />
            ))}
          </Document>
        </div>

        <div className="form-pane">
          <h3 style={{ marginTop: 0 }}>Review fields</h3>

          <div className="field">
            <label>Delivery note no. <ConfBadge value={c.deliveryNoteNo} /></label>
            <input value={form.deliveryNoteNo ?? ''}
              onChange={(e) => update('deliveryNoteNo', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Project number <ConfBadge value={c.projectNumber} /></label>
            <input value={form.projectNumber ?? ''} placeholder="PR24-1234"
              onChange={(e) => update('projectNumber', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Delivery date <ConfBadge value={c.deliveryDate} /></label>
            <input type="date" value={form.deliveryDate ?? ''}
              onChange={(e) => update('deliveryDate', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Assignee <ConfBadge value={c.assignee} /></label>
            <input value={form.assigneeRawText ?? ''}
              onChange={(e) => update('assigneeRawText', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Supplier <ConfBadge value={c.supplierName} /></label>
            <input value={form.supplierName ?? ''}
              onChange={(e) => update('supplierName', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Site / Baustelle <ConfBadge value={c.site} /></label>
            <input value={form.site ?? ''}
              onChange={(e) => update('site', e.target.value || null)} />
          </div>

          <div className="field">
            <label>Cost centre <ConfBadge value={c.costCentre} /></label>
            <input value={form.costCentre ?? ''}
              onChange={(e) => update('costCentre', e.target.value || null)} />
          </div>

          <div className="flex" style={{ marginTop: 20, gap: 8 }}>
            <button className="btn secondary" onClick={save} disabled={busy}>Save</button>
            <button className="btn" onClick={confirm} disabled={busy}>Confirm</button>
          </div>
          <div className="muted" style={{ marginTop: 8, fontSize: 12 }}>
            Confirming writes a training label to the learning-loop store and audits the change.
          </div>
        </div>
      </div>
    </div>
  );
}
