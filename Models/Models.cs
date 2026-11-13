using System.Text.Json.Serialization;

namespace STROM.Models;

public class Outlet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "Allgemein";
    public bool IsOn { get; set; }
    public int? LightLevel { get; set; }
    public int? ColorTemperature { get; set; }
    public double? ColorHue { get; set; }
    public double? ColorSaturation { get; set; }
}

public class DiscoveredDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsOn { get; set; }
    public int? LightLevel { get; set; }
    public int? ColorTemperature { get; set; }
    public double? ColorHue { get; set; }
    public double? ColorSaturation { get; set; }
}

public class ControllerInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int? BatteryPercentage { get; set; }
    public List<string> LinkedDeviceIDs { get; set; } = new();
}
