public class Asteroid
{
    public string Name { get; set; }
    public long EpochDateCloseApproach { get; set; }
    public double KilometersPerSecond { get; set; }
    public bool IsPotentiallyHazardous { get; set; }
    public double EstimatedDiameterMin { get; set; }
    public double EstimatedDiameterMax { get; set; }

    public Asteroid(string name, long epochDateCloseApproach, double kilometersPerSecond, bool isPotentiallyHazardous, double estimatedDiameterMin, double estimatedDiameterMax)
    {
        Name = name;
        EpochDateCloseApproach = epochDateCloseApproach;
        KilometersPerSecond = kilometersPerSecond;
        IsPotentiallyHazardous = isPotentiallyHazardous;
        EstimatedDiameterMin = estimatedDiameterMin;
        EstimatedDiameterMax = estimatedDiameterMax;
    }
}