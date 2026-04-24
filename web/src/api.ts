import type {
  Company,
  DeliveryNoteDetail,
  DeliveryNoteListItem,
  RetrainStatus,
  UpdateDeliveryNoteDto
} from './types';

async function http<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    }
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  listNotes: (params?: { status?: string; q?: string }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.q) qs.set('q', params.q);
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    return http<DeliveryNoteListItem[]>(`/api/delivery-notes${suffix}`);
  },

  getNote: (id: string) => http<DeliveryNoteDetail>(`/api/delivery-notes/${id}`),

  uploadNote: async (file: File): Promise<DeliveryNoteDetail> => {
    const form = new FormData();
    form.append('file', file);
    const res = await fetch('/api/delivery-notes/upload', { method: 'POST', body: form });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`${res.status}: ${text}`);
    }
    return res.json();
  },

  updateNote: (id: string, dto: UpdateDeliveryNoteDto) =>
    http<DeliveryNoteDetail>(`/api/delivery-notes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(dto)
    }),

  confirmNote: (id: string, reasonCode?: string) =>
    http<DeliveryNoteDetail>(`/api/delivery-notes/${id}/confirm`, {
      method: 'POST',
      body: JSON.stringify({ reasonCode })
    }),

  rejectNote: (id: string, reasonCode?: string) =>
    http<DeliveryNoteDetail>(`/api/delivery-notes/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reasonCode })
    }),

  retryExtraction: (id: string) =>
    http<DeliveryNoteDetail>(`/api/delivery-notes/${id}/retry-extraction`, { method: 'POST' }),

  pdfUrl: (id: string) => `/api/delivery-notes/${id}/pdf`,

  listCompanies: () => http<Company[]>('/api/companies'),

  listCompanyNotes: (id: string) =>
    http<DeliveryNoteListItem[]>(`/api/companies/${id}/delivery-notes`),

  retrainStatus: () => http<RetrainStatus>('/api/training/status'),
  triggerRetrain: (notes?: string) =>
    http<{ id: string }>('/api/training/trigger', {
      method: 'POST',
      body: JSON.stringify({ notes })
    })
};
