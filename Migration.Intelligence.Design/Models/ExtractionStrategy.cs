namespace Migration.Intelligence.Design.Models;

public enum ExtractionStrategy
{
    DirectExtraction = 0,
    ReadOnlyFirst = 1,
    EventCarveOut = 2,
    StranglerFigPhased = 3,
    DeferredDueToCoupling = 4
}
