export type DeliveryNoteStatus =
  | 'Extracting'
  | 'ReadyForReview'
  | 'Confirmed'
  | 'Rejected'
  | 'ExtractionFailed';

export interface DeliveryNoteListItem {
  id: string;
  originalFileName: string;
  deliveryNoteNo: string | null;
  projectNumber: string | null;
  deliveryDate: string | null;
  assigneeName: string | null;
  status: DeliveryNoteStatus;
  createdAt: string;
  updatedAt: string;
}

export interface DeliveryNoteDetail {
  id: string;
  originalFileName: string;
  blobPath: string;
  deliveryNoteNo: string | null;
  projectNumber: string | null;
  deliveryDate: string | null;
  assigneeCompanyId: string | null;
  assigneeCompanyName: string | null;
  assigneeRawText: string | null;
  supplierName: string | null;
  site: string | null;
  costCentre: string | null;
  fieldConfidences: Record<string, number | null>;
  modelIdUsed: string | null;
  status: DeliveryNoteStatus;
  extractionError: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  confirmedAt: string | null;
  confirmedBy: string | null;
}

export interface Company {
  id: string;
  name: string;
  externalCode: string | null;
  isActive: boolean;
  aliases: string[];
}

export interface UpdateDeliveryNoteDto {
  deliveryNoteNo: string | null;
  projectNumber: string | null;
  deliveryDate: string | null;
  assigneeCompanyId: string | null;
  assigneeRawText: string | null;
  supplierName: string | null;
  site: string | null;
  costCentre: string | null;
}

export interface RetrainStatus {
  pendingLabelCount: number;
  threshold: number;
  eligibleForAutoRetrain: boolean;
  latestRun: {
    id: string;
    startedAt: string;
    finishedAt: string | null;
    status: string;
    labelCount: number;
    promoted: boolean;
  } | null;
}
