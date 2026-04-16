using System.Text;

namespace CpdToMcdx;

/// <summary>Write regions to .cpd format</summary>
static class CpdWriter
{
    public static string Write(List<Region> regions)
    {
        var sb = new StringBuilder();

        foreach (var r in regions)
        {
            switch (r.Type)
            {
                case RegionType.Heading:
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine($"\"{r.Content}");
                    break;

                case RegionType.Text:
                    sb.AppendLine($"'{r.Content}");
                    break;

                case RegionType.Math:
                    sb.AppendLine(r.Content);
                    break;

                case RegionType.DisplayEq:
                    sb.AppendLine($"#deq {r.Content}");
                    break;

                case RegionType.Comment:
                    sb.AppendLine($"'{r.Content}");
                    break;

                case RegionType.Plot:
                case RegionType.Map:
                    sb.AppendLine(r.Content);
                    break;

                case RegionType.Directive:
                    sb.AppendLine(r.Content);
                    break;
            }
        }
        return sb.ToString();
    }
}
