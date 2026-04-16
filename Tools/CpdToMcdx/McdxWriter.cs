using System.IO.Compression;
using System.Text;
using System.Xml;

namespace CpdToMcdx;

/// <summary>Generate a .mcdx (ZIP) file from parsed regions</summary>
static class McdxWriter
{
    public static void Write(string outputPath, List<Region> regions, string version)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        var wsRelsIds = new List<(string id, string target, string type)>();
        string worksheetId = NewId(), headerId = NewId(), footerId = NewId(), coreId = NewId();

        // 1. [Content_Types].xml
        AddXml(zip, "[Content_Types].xml", w =>
        {
            w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            WriteDefault(w, "xml", "application/vnd.openxmlformats-officedocument.mathprocessingml.mathcad.main+xml");
            WriteDefault(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteDefault(w, "png", "image/png");
            WriteDefault(w, "XamlPackage", "application/zip");
            WriteOverride(w, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
            WriteOverride(w, "/docProps/app.xml", "application/mathcad.extended-properties+xml");
            WriteOverride(w, "/mathcad/settings/presentation.xml", "application/vnd.openxmlformats-officedocument.mathprocessingml.mathcad.settings.presentation+xml");
            WriteOverride(w, "/mathcad/settings/calculation.xml", "application/vnd.openxmlformats-officedocument.mathprocessingml.mathcad.settings.calculation+xml");
            WriteOverride(w, "/mathcad/result.xml", "application/vnd.openxmlformats-officedocument.mathprocessingml.mathcad.result+xml");
            w.WriteEndElement();
        });

        // 2. _rels/.rels
        AddXml(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            WriteRel(w, worksheetId, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "/mathcad/worksheet.xml");
            WriteRel(w, headerId, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "/mathcad/header.xml");
            WriteRel(w, footerId, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "/mathcad/footer.xml");
            WriteRel(w, coreId, "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "/docProps/core.xml");
            w.WriteEndElement();
        });

        // 3. docProps/core.xml
        AddXml(zip, "docProps/core.xml", w =>
        {
            w.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
            w.WriteStartElement("dc", "creator", "http://purl.org/dc/elements/1.1/");
            w.WriteString("Calcpad-Symbolic (CpdToMcdx)");
            w.WriteEndElement();
            w.WriteElementString("cp", "lastModifiedBy", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties", "CpdToMcdx");
            w.WriteElementString("cp", "revision", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties", "1");
            w.WriteEndElement();
        });

        // 4. docProps/app.xml
        var vc = VersionConfig.Get(version);
        AddXml(zip, "docProps/app.xml", w =>
        {
            w.WriteStartElement("properties", "http://schemas.mathsoft.com/extended-properties");
            w.WriteElementString("appVersion", vc.AppVersion);
            w.WriteStartElement("serializationVersion");
            w.WriteAttributeString("architecture", "x64");
            w.WriteAttributeString("Culture", "en-US");
            w.WriteAttributeString("UiCulture", "en-US");
            w.WriteString(vc.AppVersion);
            w.WriteEndElement();
            w.WriteElementString("engineVersion", vc.EngineVersion);
            w.WriteElementString("build", vc.Build);
            w.WriteStartElement("schemaPropertiesList");
            foreach (var (name, ver) in vc.Schemas)
            {
                w.WriteStartElement("schemaProperties");
                w.WriteAttributeString("name", name);
                w.WriteAttributeString("version", ver);
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        });

        // 5. mathcad/settings/calculation.xml
        AddXml(zip, "mathcad/settings/calculation.xml", w =>
        {
            w.WriteStartElement("calculation", "http://schemas.ptc.com/mathcad/settings/calculation10");
            w.WriteAttributeString("cache-worksheet", "true");
            w.WriteStartElement("builtInVariables");
            w.WriteAttributeString("array-origin", "0");
            w.WriteAttributeString("convergence-tolerance", "0.001");
            w.WriteAttributeString("constraint-tolerance", "0.001");
            w.WriteEndElement();
            w.WriteStartElement("calculationBehavior");
            w.WriteAttributeString("automatic-recalculation", "true");
            w.WriteEndElement();
            w.WriteStartElement("units");
            w.WriteStartElement("currentUnitSystem");
            w.WriteAttributeString("name", "si");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        });

        // 6. mathcad/settings/presentation.xml
        AddXml(zip, "mathcad/settings/presentation.xml", w =>
        {
            w.WriteStartElement("presentation", "http://schemas.ptc.com/mathcad/settings/presentation10");
            w.WriteStartElement("pageModel");
            w.WriteAttributeString("paper-code", "A4");
            w.WriteAttributeString("orientation", "Portrait");
            w.WriteEndElement();
            w.WriteEndElement();
        });

        // 7. mathcad/result.xml (empty)
        AddXml(zip, "mathcad/result.xml", w =>
        {
            w.WriteStartElement("results", "http://schemas.mathsoft.com/result10");
            w.WriteEndElement();
        });

        // 8. mathcad/header.xml & footer.xml (empty)
        AddXml(zip, "mathcad/header.xml", w =>
        {
            w.WriteStartElement("headerFooter", "http://schemas.mathsoft.com/worksheet50");
            w.WriteStartElement("regions"); w.WriteEndElement();
            w.WriteEndElement();
        });
        AddXml(zip, "mathcad/footer.xml", w =>
        {
            w.WriteStartElement("headerFooter", "http://schemas.mathsoft.com/worksheet50");
            w.WriteStartElement("regions"); w.WriteEndElement();
            w.WriteEndElement();
        });

        // 9. mathcad/worksheet.xml — THE MAIN CONTENT
        AddXml(zip, "mathcad/worksheet.xml", w =>
        {
            w.WriteStartElement("worksheet", "http://schemas.mathsoft.com/worksheet50");
            w.WriteAttributeString("xmlns", "ml", null, "http://schemas.mathsoft.com/math50");
            w.WriteAttributeString("xmlns", "u", null, "http://schemas.mathsoft.com/units10");
            w.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            w.WriteStartElement("regions");
            double top = 20;
            int regionId = 0;
            int resultRef = 0;

            foreach (var region in regions)
            {
                switch (region.Type)
                {
                    case RegionType.Heading:
                    case RegionType.Text:
                        var textId = NewId();
                        wsRelsIds.Add((textId, $"/mathcad/xaml/FlowDocument{regionId}.XamlPackage", "flowDocument"));
                        WriteTextRegion(w, regionId, top, textId, region);
                        top += region.Type == RegionType.Heading ? 30 : 20;
                        break;

                    case RegionType.Math:
                    case RegionType.DisplayEq:
                        WriteMathRegion(w, regionId, top, resultRef, region);
                        top += 25;
                        resultRef++;
                        break;

                    case RegionType.Comment:
                        // Skip comments in MCDX
                        break;

                    case RegionType.Plot:
                    case RegionType.Map:
                        // TODO: convert to xyPlot or image
                        break;
                }
                regionId++;
            }
            w.WriteEndElement(); // regions
            w.WriteEndElement(); // worksheet
        });

        // 10. mathcad/_rels/worksheet.xml.rels
        AddXml(zip, "mathcad/_rels/worksheet.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            foreach (var (id, target, type) in wsRelsIds)
            {
                var typeUrl = type == "flowDocument"
                    ? "http://schemas.openxmlformats.org/officeDocument/2006/relationships/flowDocument"
                    : "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
                WriteRel(w, id, typeUrl, target);
            }
            w.WriteEndElement();
        });

        // 11. Empty header/footer rels
        AddXml(zip, "mathcad/_rels/header.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            w.WriteEndElement();
        });
        AddXml(zip, "mathcad/_rels/footer.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            w.WriteEndElement();
        });

        // 12. FlowDocument XAML packages for text regions
        foreach (var (id, target, type) in wsRelsIds)
        {
            if (type != "flowDocument") continue;
            // Create minimal XamlPackage (it's a ZIP inside a ZIP)
            var regionIdx = int.Parse(System.Text.RegularExpressions.Regex.Match(target, @"\d+").Value);
            var reg = regionIdx < regions.Count ? regions[regionIdx] : null;
            var text = reg?.Content ?? "";

            var entry = zip.CreateEntry(target.TrimStart('/'));
            using var stream = entry.Open();
            // XamlPackage is a ZIP containing Document.xaml
            using var innerZip = new ZipArchive(stream, ZipArchiveMode.Create);
            var docEntry = innerZip.CreateEntry("Document.xaml");
            using var docStream = docEntry.Open();
            using var sw = new StreamWriter(docStream, Encoding.UTF8);
            var fontSize = reg?.Type == RegionType.Heading ? "18" : "14.6667";
            var fontWeight = reg?.Type == RegionType.Heading ? "Bold" : "Normal";
            sw.Write($@"<FlowDocument xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" FontFamily=""Calibri"" FontSize=""{fontSize}"" FontWeight=""{fontWeight}""><Paragraph><Run>{EscapeXml(text)}</Run></Paragraph></FlowDocument>");
        }
    }

    static void WriteTextRegion(XmlWriter w, int id, double top, string itemId, Region region)
    {
        w.WriteStartElement("region");
        w.WriteAttributeString("region-id", id.ToString());
        w.WriteAttributeString("top", top.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteAttributeString("left", "9.45");
        w.WriteStartElement("text");
        w.WriteAttributeString("item-idref", itemId);
        var fontSize = region.Type == RegionType.Heading ? "18" : "14.6667";
        var fontWeight = region.Type == RegionType.Heading ? "Bold" : "Normal";
        // FlowDocument must be written as raw XML to avoid namespace conflicts
        w.WriteRaw($"<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            $"FontFamily=\"Calibri\" FontSize=\"{fontSize}\" FontWeight=\"{fontWeight}\" Foreground=\"#FF000000\" />");
        w.WriteEndElement(); // text
        w.WriteEndElement(); // region
    }

    static void WriteMathRegion(XmlWriter w, int id, double top, int resultRef, Region region)
    {
        w.WriteStartElement("region");
        w.WriteAttributeString("region-id", id.ToString());
        w.WriteAttributeString("top", top.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteAttributeString("left", "28.35");
        w.WriteStartElement("math");
        w.WriteAttributeString("resultRef", resultRef.ToString());

        if (region.VarName != null && region.Expression != null)
        {
            w.WriteStartElement("ml", "define", "http://schemas.mathsoft.com/math50");

            if (region.FuncArgs != null)
            {
                // Function definition
                w.WriteStartElement("ml", "function", null);
                ExpressionConverter.WriteExpression(w, region.VarName);
                w.WriteStartElement("ml", "boundVars", null);
                var args = ExpressionConverter.SplitArgs(region.FuncArgs);
                foreach (var arg in args)
                {
                    w.WriteStartElement("ml", "id", null);
                    w.WriteAttributeString("labels", "VARIABLE");
                    w.WriteAttributeString("xml", "space", null, "preserve");
                    w.WriteString(arg.Trim());
                    w.WriteEndElement();
                }
                w.WriteEndElement(); // boundVars
                w.WriteEndElement(); // function
            }
            else
            {
                // Variable definition
                w.WriteStartElement("ml", "id", null);
                w.WriteAttributeString("labels", "VARIABLE");
                w.WriteAttributeString("xml", "space", null, "preserve");
                w.WriteString(region.VarName);
                w.WriteEndElement();
            }

            ExpressionConverter.WriteExpression(w, region.Expression);
            w.WriteEndElement(); // define
        }
        else if (region.Expression != null)
        {
            // Standalone expression (evaluation)
            ExpressionConverter.WriteExpression(w, region.Expression);
        }

        w.WriteEndElement(); // math
        w.WriteEndElement(); // region
    }

    // --- Helpers ---
    static string NewId() => "R" + Guid.NewGuid().ToString("N")[..15];

    static void AddXml(ZipArchive zip, string path, Action<XmlWriter> writeContent)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = false, OmitXmlDeclaration = false };
        using var w = XmlWriter.Create(stream, settings);
        w.WriteStartDocument();
        writeContent(w);
        w.WriteEndDocument();
    }

    static void WriteDefault(XmlWriter w, string ext, string contentType)
    {
        w.WriteStartElement("Default");
        w.WriteAttributeString("Extension", ext);
        w.WriteAttributeString("ContentType", contentType);
        w.WriteEndElement();
    }

    static void WriteOverride(XmlWriter w, string partName, string contentType)
    {
        w.WriteStartElement("Override");
        w.WriteAttributeString("PartName", partName);
        w.WriteAttributeString("ContentType", contentType);
        w.WriteEndElement();
    }

    static void WriteRel(XmlWriter w, string id, string type, string target)
    {
        w.WriteStartElement("Relationship");
        w.WriteAttributeString("Type", type);
        w.WriteAttributeString("Target", target);
        w.WriteAttributeString("Id", id);
        w.WriteEndElement();
    }

    static string EscapeXml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
