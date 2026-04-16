namespace CpdToMcdx;

record VersionInfo(string AppVersion, string EngineVersion, string Build, (string name, string ver)[] Schemas);

static class VersionConfig
{
    public static VersionInfo Get(string version) => version switch
    {
        "7.0" => new("7.0.0.0", "7.0.0.17", "2020.06.15.001", new[]
        {
            ("App", "1.3.4"), ("Calculation", "1.4.2"), ("Math", "5.7.0"),
            ("Presentation", "1.7.1"), ("Result", "1.8.1"), ("Worksheet", "5.5.0"),
            ("SimpleTypes", "1.3.1"), ("Units", "12.3.1"), ("Integration", "1.3.1"), ("Provenance", "1.3.1")
        }),
        "8.0" => new("8.0.0.0", "8.0.0.17", "2021.09.01.001", new[]
        {
            ("App", "1.3.4"), ("Calculation", "1.4.2"), ("Math", "5.8.0"),
            ("Presentation", "1.7.1"), ("Result", "1.8.1"), ("Worksheet", "5.6.0"),
            ("SimpleTypes", "1.3.1"), ("Units", "12.3.1"), ("Integration", "1.3.1"), ("Provenance", "1.3.1")
        }),
        "9.0" => new("9.0.0.0", "9.0.0.17", "2023.02.16.001", new[]
        {
            ("App", "1.3.4"), ("Calculation", "1.4.2"), ("Math", "5.9.0"),
            ("Presentation", "1.7.1"), ("Result", "1.8.1"), ("Worksheet", "5.7.0"),
            ("SimpleTypes", "1.3.1"), ("Units", "12.3.1"), ("Integration", "1.3.1"), ("Provenance", "1.3.1")
        }),
        "10.0" => new("10.0.0.0", "10.0.0.17", "2024.03.25.002", new[]
        {
            ("App", "1.3.4"), ("Calculation", "1.4.2"), ("Math", "5.10.6"),
            ("Presentation", "1.7.1"), ("Result", "1.8.1"), ("Worksheet", "5.10.4"),
            ("SimpleTypes", "1.3.1"), ("Units", "12.3.1"), ("Integration", "1.3.1"), ("Provenance", "1.3.1")
        }),
        _ => Get("9.0") // Default
    };
}
