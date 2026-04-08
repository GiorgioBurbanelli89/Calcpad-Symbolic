// chart.ts — Gráficas 2D interactivas con Plotly.js
// Líneas, scatter, barras, fill, con zoom, pan, tooltips, export

import Plotly from "plotly.js-dist-min";

// Datos de entrada (misma interfaz que antes)
export interface ChartData {
  series: ChartSeries[];
  options?: ChartOptions;
}

export interface ChartSeries {
  x: number[];
  y: number[];
  label?: string;
  color?: string;
  type?: "line" | "scatter" | "bar" | "fill";
}

export interface ChartOptions {
  width?: number;
  height?: number;
  title?: string;
  xLabel?: string;
  yLabel?: string;
  grid?: boolean;
}

const autoColors = [
  "#1f77b4", "#d62728", "#2ca02c", "#ff7f0e",
  "#9467bd", "#17becf", "#e377c2", "#7f7f7f"
];

export function chart(containerId: string, data: ChartData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 700;
  const H = opts.height || 400;

  // Build Plotly traces
  const traces: Plotly.Data[] = data.series.map((s, i) => {
    const color = s.color || autoColors[i % autoColors.length];
    const type = s.type || "line";

    if (type === "bar") {
      return {
        x: s.x,
        y: s.y,
        name: s.label || `Serie ${i + 1}`,
        type: "bar" as const,
        marker: { color },
      };
    }

    if (type === "fill") {
      return {
        x: s.x,
        y: s.y,
        name: s.label || `Serie ${i + 1}`,
        type: "scatter" as const,
        mode: "lines" as const,
        fill: "tozeroy" as const,
        fillcolor: color + "33",
        line: { color, width: 2 },
      };
    }

    if (type === "scatter") {
      return {
        x: s.x,
        y: s.y,
        name: s.label || `Serie ${i + 1}`,
        type: "scatter" as const,
        mode: "markers" as const,
        marker: { color, size: 6 },
      };
    }

    // default: line
    return {
      x: s.x,
      y: s.y,
      name: s.label || `Serie ${i + 1}`,
      type: "scatter" as const,
      mode: "lines+markers" as const,
      line: { color, width: 2 },
      marker: { color, size: 4 },
    };
  });

  const layout: Partial<Plotly.Layout> = {
    width: W,
    height: H,
    title: opts.title ? { text: opts.title, font: { size: 16 } } : undefined,
    xaxis: {
      title: opts.xLabel || "",
      gridcolor: "#e0e0e0",
      zeroline: true,
      zerolinecolor: "#999",
    },
    yaxis: {
      title: opts.yLabel || "",
      gridcolor: "#e0e0e0",
      zeroline: true,
      zerolinecolor: "#999",
    },
    showlegend: data.series.length > 1,
    legend: { x: 0, y: -0.25, orientation: "h" as const, font: { size: 11 } },
    margin: { l: 60, r: 20, t: opts.title ? 50 : 20, b: data.series.length > 1 ? 80 : 50 },
    paper_bgcolor: "white",
    plot_bgcolor: "white",
    font: { family: "Segoe UI, sans-serif", size: 12 },
    hovermode: "closest" as const,
  };

  const config: Partial<Plotly.Config> = {
    responsive: false,
    displayModeBar: true,
    modeBarButtonsToRemove: ["lasso2d", "select2d", "autoScale2d"],
    displaylogo: false,
    toImageButtonOptions: {
      format: "png",
      filename: opts.title || "calcpad_chart",
      width: W * 2,
      height: H * 2,
      scale: 2,
    },
  };

  Plotly.newPlot(container, traces, layout, config);
}
