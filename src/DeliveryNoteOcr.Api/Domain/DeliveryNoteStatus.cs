namespace DeliveryNoteOcr.Api.Domain;

public enum DeliveryNoteStatus
{
    Extracting = 0,
    ReadyForReview = 1,
    Confirmed = 2,
    Rejected = 3,
    ExtractionFailed = 4
}
