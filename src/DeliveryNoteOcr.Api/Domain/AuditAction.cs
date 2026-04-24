namespace DeliveryNoteOcr.Api.Domain;

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Confirmed = 2,
    Rejected = 3,
    Deleted = 4,
    ExtractionCompleted = 5,
    ExtractionFailed = 6
}

public enum AuditSource
{
    Manual = 0,
    Ocr = 1,
    Api = 2,
    Import = 3,
    System = 4
}
