namespace DeliveryNoteOcr.Api.Services.Training;

public class TrainingOptions
{
    public string StorePath { get; set; } = "data/training";
    public int AutoRetrainThreshold { get; set; } = 25;
}
