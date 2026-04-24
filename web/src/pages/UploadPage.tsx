import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';

export default function UploadPage() {
  const [drag, setDrag] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const nav = useNavigate();

  const upload = useCallback(async (file: File) => {
    setBusy(true);
    setError(null);
    try {
      const note = await api.uploadNote(file);
      nav(`/notes/${note.id}`);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }, [nav]);

  return (
    <div>
      <h2>Upload delivery note</h2>
      {error && <div className="banner error">{error}</div>}

      <div
        className={`upload-drop${drag ? ' drag' : ''}`}
        onDragOver={(e) => { e.preventDefault(); setDrag(true); }}
        onDragLeave={() => setDrag(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDrag(false);
          const file = e.dataTransfer.files?.[0];
          if (file) upload(file);
        }}
        onClick={() => document.getElementById('file-input')?.click()}
      >
        <div style={{ fontSize: 16, marginBottom: 8 }}>
          {busy ? 'Uploading and extracting…' : 'Drop a PDF here, or click to choose'}
        </div>
        <div className="muted">PDF · up to 25 MB</div>
        <input
          id="file-input"
          type="file"
          accept="application/pdf"
          style={{ display: 'none' }}
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) upload(file);
          }}
        />
      </div>
    </div>
  );
}
