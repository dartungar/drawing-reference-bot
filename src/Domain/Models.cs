public enum ImageSource
{
    Unsplash,
    Pexels
}

public readonly record struct DrawingReferenceResult(
    string ImageUrl,
    string PhotoPageUrl,
    string PhotographerName,
    string PhotographerProfileUrl,
    ImageSource Source);
