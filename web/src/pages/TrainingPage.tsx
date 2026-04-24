import { useEffect, useState } from 'react';
import { api } from '../api';
import type { RetrainStatus } from '../types';

export default function TrainingPage() {
  const [status, setStatus] = useState<RetrainStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = () =>
    api.retrainStatus().then(setStatus).catch((e) => setError(String(e)));

  useEffect(() => { load(); }, []);

  const trigger = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.triggerRetrain('Manual trigger from UI');
      await load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Learning loop</h2>
      {error && <div className="banner error">{error}</div>}

      {status ? (
        <>
          <div className="panel" style={{ marginBottom: 12 }}>
            <div className="flex space-between">
              <div>
                <div className="muted" style={{ fontSize: 12 }}>Pending labels</div>
                <div style={{ fontSize: 24, fontWeight: 600 }}>
                  {status.pendingLabelCount} <span className="muted" style={{ fontSize: 14 }}>
                    / threshold {status.threshold}
                  </span>
                </div>
              </div>
              <button className="btn" disabled={busy || status.pendingLabelCount === 0}
                onClick={trigger}>
                Queue training run
              </button>
            </div>
            {status.eligibleForAutoRetrain && (
              <div className="banner info" style={{ marginTop: 12 }}>
                Threshold reached — an auto-retrain would fire on the next scheduled run.
              </div>
            )}
          </div>

          <h3>Latest run</h3>
          {status.latestRun ? (
            <table>
              <thead>
                <tr><th>Started</th><th>Status</th><th>Labels</th><th>Promoted</th></tr>
              </thead>
              <tbody>
                <tr>
                  <td>{new Date(status.latestRun.startedAt).toLocaleString()}</td>
                  <td>{status.latestRun.status}</td>
                  <td>{status.latestRun.labelCount}</td>
                  <td>{status.latestRun.promoted ? 'Yes' : 'No'}</td>
                </tr>
              </tbody>
            </table>
          ) : (
            <div className="muted">No training runs yet.</div>
          )}
        </>
      ) : (
        <div className="muted">Loading…</div>
      )}
    </div>
  );
}
