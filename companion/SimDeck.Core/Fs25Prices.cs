namespace SimDeck.Core;

public sealed record Fs25PriceOffer(string Crop, string CropName, string Station, double PricePer1000, string Formatted);
public sealed record Fs25Prices(IReadOnlyList<Fs25PriceOffer> Offers, long AgeMs);
