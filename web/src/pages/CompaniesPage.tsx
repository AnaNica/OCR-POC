import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import type { Company, DeliveryNoteListItem } from '../types';

export default function CompaniesPage() {
  const [items, setItems] = useState<Company[]>([]);
  const [name, setName] = useState('');
  const [aliases, setAliases] = useState('');
  const [error, setError] = useState<string | null>(null);

  const [selected, setSelected] = useState<Company | null>(null);
  const [notes, setNotes] = useState<DeliveryNoteListItem[] | null>(null);
  const [notesLoading, setNotesLoading] = useState(false);

  const load = () => api.listCompanies().then(setItems).catch((e) => setError(String(e)));
  useEffect(() => { load(); }, []);

  const select = async (c: Company) => {
    setSelected(c);
    setNotes(null);
    setNotesLoading(true);
    try {
      setNotes(await api.listCompanyNotes(c.id));
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setNotesLoading(false);
    }
  };

  const add = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const res = await fetch('/api/companies', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          aliases: aliases.split(',').map((a) => a.trim()).filter(Boolean)
        })
      });
      if (!res.ok) throw new Error(await res.text());
      setName('');
      setAliases('');
      load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div>
      <h2>Companies (assignee list)</h2>
      {error && <div className="banner error">{error}</div>}

      <div className="panel" style={{ marginBottom: 16 }}>
        <form onSubmit={add} className="flex" style={{ gap: 8 }}>
          <input placeholder="Company name" value={name}
            onChange={(e) => setName(e.target.value)} required
            style={{ flex: 1, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }} />
          <input placeholder="Aliases (comma-separated)" value={aliases}
            onChange={(e) => setAliases(e.target.value)}
            style={{ flex: 2, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }} />
          <button className="btn" type="submit">Add</button>
        </form>
      </div>

      <table>
        <thead>
          <tr><th>Name</th><th>Aliases</th><th>Active</th></tr>
        </thead>
        <tbody>
          {items.map((c) => (
            <tr key={c.id}
                onClick={() => select(c)}
                style={{
                  cursor: 'pointer',
                  background: selected?.id === c.id ? '#eff6ff' : undefined
                }}>
              <td>{c.name}</td>
              <td>{c.aliases.join(', ') || <span className="muted">—</span>}</td>
              <td>{c.isActive ? 'Yes' : 'No'}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={3} className="muted" style={{ textAlign: 'center', padding: 24 }}>
              No companies yet. They're auto-created when you confirm a delivery note.
            </td></tr>
          )}
        </tbody>
      </table>

      {selected && (
        <div style={{ marginTop: 24 }}>
          <h3 style={{ marginBottom: 8 }}>
            Documents for <em>{selected.name}</em>
            <button className="btn secondary" style={{ marginLeft: 12, padding: '2px 10px' }}
              onClick={() => { setSelected(null); setNotes(null); }}>
              Clear
            </button>
          </h3>

          {notesLoading && <div className="muted">Loading…</div>}

          {!notesLoading && notes && notes.length === 0 && (
            <div className="muted">No documents linked to this company yet.</div>
          )}

          {!notesLoading && notes && notes.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>File</th>
                  <th>Delivery No</th>
                  <th>Project</th>
                  <th>Date</th>
                  <th>Status</th>
                  <th>Updated</th>
                </tr>
              </thead>
              <tbody>
                {notes.map((n) => (
                  <tr key={n.id}>
                    <td><Link to={`/notes/${n.id}`}>{n.originalFileName}</Link></td>
                    <td>{n.deliveryNoteNo ?? <span className="muted">—</span>}</td>
                    <td>{n.projectNumber ?? <span className="muted">—</span>}</td>
                    <td>{n.deliveryDate ?? <span className="muted">—</span>}</td>
                    <td><span className={`status-pill status-${n.status}`}>{n.status}</span></td>
                    <td className="muted">{new Date(n.updatedAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
