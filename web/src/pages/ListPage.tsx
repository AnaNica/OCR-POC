import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import type { DeliveryNoteListItem } from '../types';

export default function ListPage() {
  const [items, setItems] = useState<DeliveryNoteListItem[]>([]);
  const [q, setQ] = useState('');
  const [status, setStatus] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.listNotes({ q, status });
      setItems(data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  return (
    <div>
      <div className="flex space-between" style={{ marginBottom: 16 }}>
        <h2 style={{ margin: 0 }}>Delivery Notes</h2>
        <Link to="/upload" className="btn">Upload</Link>
      </div>

      <div className="panel" style={{ marginBottom: 16 }}>
        <form
          onSubmit={(e) => { e.preventDefault(); load(); }}
          className="flex"
          style={{ gap: 12 }}
        >
          <input
            placeholder="Search (delivery note no, project, filename)"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            style={{ flex: 1, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
          />
          <select value={status} onChange={(e) => setStatus(e.target.value)}
            style={{ padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}>
            <option value="">All statuses</option>
            <option>Extracting</option>
            <option>ReadyForReview</option>
            <option>Confirmed</option>
            <option>Rejected</option>
            <option>ExtractionFailed</option>
          </select>
          <button type="submit" className="btn">Search</button>
        </form>
      </div>

      {error && <div className="banner error">{error}</div>}
      {loading && <div className="muted">Loading…</div>}

      {!loading && (
        <table>
          <thead>
            <tr>
              <th>File</th>
              <th>Delivery No</th>
              <th>Project</th>
              <th>Date</th>
              <th>Assignee</th>
              <th>Status</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {items.map((n) => (
              <tr key={n.id}>
                <td><Link to={`/notes/${n.id}`}>{n.originalFileName}</Link></td>
                <td>{n.deliveryNoteNo ?? <span className="muted">—</span>}</td>
                <td>{n.projectNumber ?? <span className="muted">—</span>}</td>
                <td>{n.deliveryDate ?? <span className="muted">—</span>}</td>
                <td>{n.assigneeName ?? <span className="muted">—</span>}</td>
                <td><span className={`status-pill status-${n.status}`}>{n.status}</span></td>
                <td className="muted">{new Date(n.updatedAt).toLocaleString()}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={7} className="muted" style={{ textAlign: 'center', padding: 32 }}>
                No delivery notes yet. Upload one to get started.
              </td></tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
