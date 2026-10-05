namespace Avis.App.Simulation;

/// <summary>
/// Simulation mode: runs the whole station flow on a development PC with no
/// badge scanner, JabilEye camera, LightGuide or iFactory - everything is driven
/// from the SIMULATOR tab. Turned on by the "AVIS (Simulation)" launch profile
/// (appsettings.Simulation.json). Never enable it on a production station.
/// </summary>
public class SimulationOptions
{
    public const string SectionName = "Simulation";

    public bool Enabled { get; set; }

    /// <summary>Material (part number) the simulated iFactory returns for every Asset ID.</summary>
    public string Material { get; set; } = "SIM-PN-001";

    /// <summary>Asset IDs containing this text are "not found" in the simulated iFactory.</summary>
    public string UnknownAssetMarker { get; set; } = "UNKNOWN";
}
