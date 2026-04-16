using System.Collections.Generic;

namespace Calcpad.Core.Plugins
{
    /// <summary>
    /// Interface for Calcpad Symbolic plugins.
    /// Plugins are loaded from the Plugins/ folder as DLLs.
    /// </summary>
    public interface ICalcpadPlugin
    {
        /// <summary>Plugin display name (e.g., "MathCad Converter")</summary>
        string Name { get; }

        /// <summary>Plugin version</summary>
        string Version { get; }

        /// <summary>Menu text for export (e.g., "Export to MathCad (.mcdx)")</summary>
        string ExportMenuText { get; }

        /// <summary>Menu text for import (e.g., "Import from MathCad (.mcdx)")</summary>
        string ImportMenuText { get; }

        /// <summary>File filter for dialogs (e.g., "MathCad Prime|*.mcdx")</summary>
        string FileFilter { get; }

        /// <summary>Default file extension (e.g., ".mcdx")</summary>
        string DefaultExtension { get; }

        /// <summary>Whether this plugin supports export (CPD → target format)</summary>
        bool CanExport { get; }

        /// <summary>Whether this plugin supports import (target format → CPD)</summary>
        bool CanImport { get; }

        /// <summary>Toolbar button tooltip</summary>
        string ToolTip { get; }

        /// <summary>
        /// Export CPD content to the target format.
        /// </summary>
        /// <param name="cpdContent">The .cpd file content</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="options">Optional parameters (e.g., version="9.0")</param>
        /// <returns>Status message</returns>
        string Export(string cpdContent, string outputPath, Dictionary<string, string>? options = null);

        /// <summary>
        /// Import from target format to CPD content.
        /// </summary>
        /// <param name="inputPath">Input file path</param>
        /// <returns>CPD content string</returns>
        string Import(string inputPath);

        /// <summary>
        /// Generate HTML preview of the content.
        /// </summary>
        /// <param name="content">CPD content or file path</param>
        /// <param name="title">Document title</param>
        /// <returns>HTML string</returns>
        string GenerateHtmlPreview(string content, string title);
    }
}
