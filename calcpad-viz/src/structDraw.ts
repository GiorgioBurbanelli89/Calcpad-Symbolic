// structDraw.ts — Structural diagrams for FEM education
// Draws springs, bars, beams, supports, forces using Canvas2D

export interface StructDrawData {
  elements: StructElement[];
  options?: StructOptions;
}

export interface StructElement {
  type: "spring" | "bar" | "beam" | "node" | "support" | "force" | "moment" | "label" | "dim";
  x1?: number; y1?: number;
  x2?: number; y2?: number;
  x?: number; y?: number;
  value?: number;
  text?: string;
  color?: string;
  supportType?: "pin" | "fixed" | "roller";
  dir?: "up" | "down" | "left" | "right";
}

export interface StructOptions {
  width?: number;
  height?: number;
  title?: string;
  scale?: number;
  padding?: number;
}

export function structDraw(containerId: string, data: StructDrawData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 700;
  const H = opts.height || 300;
  const pad = opts.padding || 50;

  const canvas = document.createElement("canvas");
  canvas.width = W;
  canvas.height = H;
  canvas.style.cssText = "border:1px solid #ddd; border-radius:4px; background:white;";
  container.appendChild(canvas);

  const ctx = canvas.getContext("2d")!;
  ctx.textBaseline = "middle";
  ctx.textAlign = "center";

  // Auto-scale from element coordinates to canvas
  let xmin = Infinity, xmax = -Infinity, ymin = Infinity, ymax = -Infinity;
  for (const el of data.elements) {
    if (el.x1 !== undefined) { xmin = Math.min(xmin, el.x1); xmax = Math.max(xmax, el.x1); }
    if (el.x2 !== undefined) { xmin = Math.min(xmin, el.x2); xmax = Math.max(xmax, el.x2); }
    if (el.x !== undefined) { xmin = Math.min(xmin, el.x); xmax = Math.max(xmax, el.x); }
    if (el.y1 !== undefined) { ymin = Math.min(ymin, el.y1); ymax = Math.max(ymax, el.y1); }
    if (el.y2 !== undefined) { ymin = Math.min(ymin, el.y2); ymax = Math.max(ymax, el.y2); }
    if (el.y !== undefined) { ymin = Math.min(ymin, el.y); ymax = Math.max(ymax, el.y); }
  }
  if (xmin === Infinity) { xmin = 0; xmax = 10; ymin = 0; ymax = 5; }
  const dx = xmax - xmin || 1;
  const dy = ymax - ymin || 1;
  const sc = Math.min((W - 2 * pad) / dx, (H - 2 * pad) / dy) * 0.85;
  const offX = W / 2 - (xmin + xmax) / 2 * sc;
  const offY = H / 2 + (ymin + ymax) / 2 * sc;

  function tx(x: number) { return offX + x * sc; }
  function ty(y: number) { return offY - y * sc; }

  // Title
  if (opts.title) {
    ctx.font = "bold 14px sans-serif";
    ctx.fillStyle = "#333";
    ctx.fillText(opts.title, W / 2, 18);
  }

  // Draw each element
  for (const el of data.elements) {
    const color = el.color || "#333";

    switch (el.type) {
      case "spring":
        drawSpring(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), color);
        if (el.text) {
          ctx.font = "12px sans-serif";
          ctx.fillStyle = "#666";
          const mx = (tx(el.x1!) + tx(el.x2!)) / 2;
          const my = (ty(el.y1!) + ty(el.y2!)) / 2 - 20;
          ctx.fillText(el.text, mx, my);
        }
        break;

      case "bar":
        drawBar(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), color);
        if (el.text) {
          ctx.font = "12px sans-serif";
          ctx.fillStyle = "#666";
          const mx = (tx(el.x1!) + tx(el.x2!)) / 2;
          const my = (ty(el.y1!) + ty(el.y2!)) / 2 - 15;
          ctx.fillText(el.text, mx, my);
        }
        break;

      case "beam":
        drawBeam(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), color);
        if (el.text) {
          ctx.font = "12px sans-serif";
          ctx.fillStyle = "#666";
          const mx = (tx(el.x1!) + tx(el.x2!)) / 2;
          const my = (ty(el.y1!) + ty(el.y2!)) / 2 - 18;
          ctx.fillText(el.text, mx, my);
        }
        break;

      case "node":
        ctx.beginPath();
        ctx.arc(tx(el.x!), ty(el.y!), 5, 0, Math.PI * 2);
        ctx.fillStyle = color;
        ctx.fill();
        ctx.strokeStyle = "#333";
        ctx.lineWidth = 1;
        ctx.stroke();
        if (el.text) {
          ctx.font = "bold 11px sans-serif";
          ctx.fillStyle = "#333";
          ctx.fillText(el.text, tx(el.x!), ty(el.y!) - 14);
        }
        break;

      case "support":
        drawSupport(ctx, tx(el.x!), ty(el.y!), el.supportType || "pin", sc);
        break;

      case "force":
        drawForce(ctx, tx(el.x!), ty(el.y!), el.dir || "down", el.value || 0, el.text || "", sc);
        break;

      case "moment":
        drawMoment(ctx, tx(el.x!), ty(el.y!), el.value || 1, el.text || "");
        break;

      case "label":
        ctx.font = el.color ? `bold 13px sans-serif` : "12px sans-serif";
        ctx.fillStyle = color;
        ctx.fillText(el.text || "", tx(el.x!), ty(el.y!));
        break;

      case "dim":
        drawDimension(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), el.text || "");
        break;
    }
  }
}

// ─── Drawing primitives ───

function drawSpring(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number, color: string) {
  const len = Math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2);
  const angle = Math.atan2(y2 - y1, x2 - x1);
  const nCoils = 6;
  const coilH = 10;
  const straight = len * 0.15;

  ctx.save();
  ctx.translate(x1, y1);
  ctx.rotate(angle);
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(straight, 0);
  const coilLen = len - 2 * straight;
  const step = coilLen / (nCoils * 2);
  for (let i = 0; i < nCoils * 2; i++) {
    const px = straight + (i + 1) * step;
    const py = (i % 2 === 0 ? -coilH : coilH);
    ctx.lineTo(px, py);
  }
  ctx.lineTo(len, 0);
  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.restore();
}

function drawBar(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number, color: string) {
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.strokeStyle = color;
  ctx.lineWidth = 4;
  ctx.stroke();

  // Cross-hatch pattern
  const len = Math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2);
  const angle = Math.atan2(y2 - y1, x2 - x1);
  const nHatch = Math.floor(len / 15);
  ctx.save();
  ctx.translate(x1, y1);
  ctx.rotate(angle);
  ctx.strokeStyle = "#999";
  ctx.lineWidth = 0.5;
  for (let i = 1; i <= nHatch; i++) {
    const px = i * len / (nHatch + 1);
    ctx.beginPath();
    ctx.moveTo(px, -6);
    ctx.lineTo(px, 6);
    ctx.stroke();
  }
  ctx.restore();
}

function drawBeam(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number, color: string) {
  const angle = Math.atan2(y2 - y1, x2 - x1);
  const len = Math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2);
  const h = 8;

  ctx.save();
  ctx.translate(x1, y1);
  ctx.rotate(angle);

  // Filled beam rectangle
  ctx.fillStyle = "#e8e8f0";
  ctx.fillRect(0, -h, len, h * 2);
  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.strokeRect(0, -h, len, h * 2);

  // Center line
  ctx.setLineDash([4, 4]);
  ctx.strokeStyle = "#aaa";
  ctx.lineWidth = 0.5;
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(len, 0);
  ctx.stroke();
  ctx.setLineDash([]);

  ctx.restore();
}

function drawSupport(ctx: CanvasRenderingContext2D, x: number, y: number, type: string, sc: number) {
  const sz = Math.max(12, sc * 0.15);

  if (type === "pin") {
    // Triangle
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x - sz * 0.7, y + sz);
    ctx.lineTo(x + sz * 0.7, y + sz);
    ctx.closePath();
    ctx.fillStyle = "#ddd";
    ctx.fill();
    ctx.strokeStyle = "#333";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    // Ground line
    ctx.beginPath();
    ctx.moveTo(x - sz, y + sz);
    ctx.lineTo(x + sz, y + sz);
    ctx.stroke();
    // Hatching
    for (let i = -3; i <= 3; i++) {
      ctx.beginPath();
      ctx.moveTo(x + i * sz / 3, y + sz);
      ctx.lineTo(x + i * sz / 3 - 4, y + sz + 6);
      ctx.strokeStyle = "#666";
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  } else if (type === "fixed") {
    // Wall
    ctx.fillStyle = "#ccc";
    ctx.fillRect(x - 3, y - sz, 6, sz * 2);
    ctx.strokeStyle = "#333";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(x, y - sz);
    ctx.lineTo(x, y + sz);
    ctx.stroke();
    // Hatching
    for (let i = -3; i <= 3; i++) {
      ctx.beginPath();
      ctx.moveTo(x - 3, y + i * sz / 3);
      ctx.lineTo(x - 10, y + i * sz / 3 + 4);
      ctx.strokeStyle = "#666";
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  } else if (type === "roller") {
    // Triangle + circle
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x - sz * 0.7, y + sz * 0.7);
    ctx.lineTo(x + sz * 0.7, y + sz * 0.7);
    ctx.closePath();
    ctx.fillStyle = "#ddd";
    ctx.fill();
    ctx.strokeStyle = "#333";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    // Circle
    ctx.beginPath();
    ctx.arc(x, y + sz * 0.7 + 5, 4, 0, Math.PI * 2);
    ctx.stroke();
    // Ground
    ctx.beginPath();
    ctx.moveTo(x - sz, y + sz * 0.7 + 10);
    ctx.lineTo(x + sz, y + sz * 0.7 + 10);
    ctx.stroke();
  }
}

function drawForce(ctx: CanvasRenderingContext2D, x: number, y: number, dir: string, value: number, text: string, sc: number) {
  const arrowLen = Math.max(30, sc * 0.3);
  const headLen = 10;
  let dx = 0, dy = 0;

  switch (dir) {
    case "down": dy = arrowLen; break;
    case "up": dy = -arrowLen; break;
    case "right": dx = arrowLen; break;
    case "left": dx = -arrowLen; break;
  }

  // Arrow line (from tail to tip at node)
  const tailX = x - dx, tailY = y - dy;
  ctx.beginPath();
  ctx.moveTo(tailX, tailY);
  ctx.lineTo(x, y);
  ctx.strokeStyle = "#e44";
  ctx.lineWidth = 2.5;
  ctx.stroke();

  // Arrowhead
  const angle = Math.atan2(dy, dx);
  ctx.beginPath();
  ctx.moveTo(x, y);
  ctx.lineTo(x - headLen * Math.cos(angle - 0.4), y - headLen * Math.sin(angle - 0.4));
  ctx.lineTo(x - headLen * Math.cos(angle + 0.4), y - headLen * Math.sin(angle + 0.4));
  ctx.closePath();
  ctx.fillStyle = "#e44";
  ctx.fill();

  // Label
  if (text) {
    ctx.font = "bold 12px sans-serif";
    ctx.fillStyle = "#c33";
    const lx = tailX + (dir === "left" ? -10 : dir === "right" ? 10 : 0);
    const ly = tailY + (dir === "up" ? -10 : dir === "down" ? -10 : -15);
    ctx.fillText(text, lx, ly);
  }
}

function drawMoment(ctx: CanvasRenderingContext2D, x: number, y: number, value: number, text: string) {
  const r = 18;
  const dir = value >= 0 ? 1 : -1;
  ctx.beginPath();
  ctx.arc(x, y, r, -Math.PI * 0.8 * dir, Math.PI * 0.6 * dir, dir < 0);
  ctx.strokeStyle = "#c33";
  ctx.lineWidth = 2;
  ctx.stroke();

  // Arrowhead at end
  const endAngle = Math.PI * 0.6 * dir;
  const ex = x + r * Math.cos(endAngle);
  const ey = y + r * Math.sin(endAngle);
  const tangent = endAngle + Math.PI / 2 * dir;
  ctx.beginPath();
  ctx.moveTo(ex, ey);
  ctx.lineTo(ex - 8 * Math.cos(tangent - 0.5), ey - 8 * Math.sin(tangent - 0.5));
  ctx.lineTo(ex - 8 * Math.cos(tangent + 0.5), ey - 8 * Math.sin(tangent + 0.5));
  ctx.closePath();
  ctx.fillStyle = "#c33";
  ctx.fill();

  if (text) {
    ctx.font = "bold 11px sans-serif";
    ctx.fillStyle = "#c33";
    ctx.fillText(text, x, y - r - 10);
  }
}

function drawDimension(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number, text: string) {
  const offset = 20;
  const dy1 = y1 + offset, dy2 = y2 + offset;

  // Extension lines
  ctx.beginPath();
  ctx.setLineDash([2, 2]);
  ctx.moveTo(x1, y1 + 5);
  ctx.lineTo(x1, dy1 + 5);
  ctx.moveTo(x2, y2 + 5);
  ctx.lineTo(x2, dy2 + 5);
  ctx.strokeStyle = "#888";
  ctx.lineWidth = 0.8;
  ctx.stroke();
  ctx.setLineDash([]);

  // Dimension line with arrows
  ctx.beginPath();
  ctx.moveTo(x1, dy1);
  ctx.lineTo(x2, dy2);
  ctx.strokeStyle = "#888";
  ctx.lineWidth = 1;
  ctx.stroke();

  // Arrows
  ctx.beginPath();
  ctx.moveTo(x1, dy1);
  ctx.lineTo(x1 + 6, dy1 - 3);
  ctx.lineTo(x1 + 6, dy1 + 3);
  ctx.closePath();
  ctx.fillStyle = "#888";
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x2, dy2);
  ctx.lineTo(x2 - 6, dy2 - 3);
  ctx.lineTo(x2 - 6, dy2 + 3);
  ctx.closePath();
  ctx.fill();

  // Text
  ctx.font = "11px sans-serif";
  ctx.fillStyle = "#555";
  ctx.fillText(text, (x1 + x2) / 2, (dy1 + dy2) / 2 - 8);
}
