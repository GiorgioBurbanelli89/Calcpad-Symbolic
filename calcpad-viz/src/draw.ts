// draw.ts — Interactive 2D drawing canvas with pan/zoom
// DSL elements: line, rect, circle, ellipse, arrow, darrow, text, polyline,
//               polygon, fillrect, dashed, dim, hdim, vdim, hatch, arc

export interface DrawData {
  elements: DrawElement[];
  options?: DrawOptions;
}

export interface DrawElement {
  type: string;
  // coordinates (world space)
  x1?: number; y1?: number;
  x2?: number; y2?: number;
  x?: number; y?: number;
  r?: number;
  rx?: number; ry?: number;
  // polyline/polygon points
  points?: number[];
  // text
  text?: string;
  // styling
  color?: string;
  fill?: string;
  lw?: number;
  fontSize?: number;
  fontWeight?: string;
  textAnchor?: "start" | "middle" | "end";
  // dashed
  dashLen?: number;
  // dim
  offset?: number;
  // arc
  startAngle?: number;
  endAngle?: number;
  // arrow head size
  headSize?: number;
}

export interface DrawOptions {
  width?: number;
  height?: number;
  title?: string;
  bg?: string;
  padding?: number;
  flipY?: boolean; // default true: Y-up like math (not SVG)
  grid?: boolean;
  gridStep?: number;
}

export function draw(containerId: string, data: DrawData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 600;
  const H = opts.height || 400;
  const pad = opts.padding || 30;
  const flipY = opts.flipY !== false; // default true
  const bg = opts.bg || "#fafafa";

  // Create canvas
  const canvas = document.createElement("canvas");
  canvas.width = W;
  canvas.height = H;
  canvas.style.cssText = "border:1px solid #ddd; border-radius:4px; cursor:grab; touch-action:none;";
  container.appendChild(canvas);

  const ctx = canvas.getContext("2d")!;

  // Compute bounding box from all elements
  let xmin = Infinity, xmax = -Infinity, ymin = Infinity, ymax = -Infinity;

  function expandBBox(x: number, y: number) {
    if (isFinite(x) && isFinite(y)) {
      xmin = Math.min(xmin, x); xmax = Math.max(xmax, x);
      ymin = Math.min(ymin, y); ymax = Math.max(ymax, y);
    }
  }

  for (const el of data.elements) {
    if (el.x1 !== undefined && el.y1 !== undefined) expandBBox(el.x1, el.y1);
    if (el.x2 !== undefined && el.y2 !== undefined) expandBBox(el.x2, el.y2);
    if (el.x !== undefined && el.y !== undefined) {
      const r = el.r || el.rx || 0;
      expandBBox(el.x - r, el.y - r);
      expandBBox(el.x + r, el.y + r);
    }
    if (el.points) {
      for (let i = 0; i < el.points.length - 1; i += 2) {
        expandBBox(el.points[i], el.points[i + 1]);
      }
    }
  }

  if (xmin === Infinity) { xmin = 0; xmax = 100; ymin = 0; ymax = 100; }
  // Add small margin if range is zero
  if (xmax - xmin < 1e-10) { xmin -= 1; xmax += 1; }
  if (ymax - ymin < 1e-10) { ymin -= 1; ymax += 1; }

  const dx = xmax - xmin;
  const dy = ymax - ymin;

  // Pan/zoom state
  let scale = Math.min((W - 2 * pad) / dx, (H - 2 * pad) / dy) * 0.9;
  let panX = W / 2 - (xmin + xmax) / 2 * scale;
  let panY = flipY
    ? H / 2 + (ymin + ymax) / 2 * scale
    : H / 2 - (ymin + ymax) / 2 * scale;

  // World → screen
  function tx(x: number) { return panX + x * scale; }
  function ty(y: number) { return flipY ? panY - y * scale : panY + y * scale; }

  // ───── Drawing ─────
  function render() {
    ctx.clearRect(0, 0, W, H);

    // Background
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, W, H);

    // Grid
    if (opts.grid) {
      const step = opts.gridStep || autoGridStep(dx, scale);
      ctx.strokeStyle = "#e0e0e0";
      ctx.lineWidth = 0.5;
      const x0 = Math.floor(xmin / step) * step;
      const y0 = Math.floor(ymin / step) * step;
      for (let gx = x0; gx <= xmax + step; gx += step) {
        const sx = tx(gx);
        if (sx < 0 || sx > W) continue;
        ctx.beginPath(); ctx.moveTo(sx, 0); ctx.lineTo(sx, H); ctx.stroke();
      }
      for (let gy = y0; gy <= ymax + step; gy += step) {
        const sy = ty(gy);
        if (sy < 0 || sy > H) continue;
        ctx.beginPath(); ctx.moveTo(0, sy); ctx.lineTo(W, sy); ctx.stroke();
      }
    }

    // Title
    if (opts.title) {
      ctx.font = "bold 13px sans-serif";
      ctx.fillStyle = "#333";
      ctx.textAlign = "center";
      ctx.textBaseline = "top";
      ctx.fillText(opts.title, W / 2, 6);
    }

    // Draw elements
    for (const el of data.elements) {
      const color = el.color || "black";
      const lw = el.lw || 1.5;
      ctx.strokeStyle = color;
      ctx.fillStyle = el.fill || color;
      ctx.lineWidth = lw;
      ctx.setLineDash([]);

      switch (el.type) {
        case "line":
          ctx.beginPath();
          ctx.moveTo(tx(el.x1!), ty(el.y1!));
          ctx.lineTo(tx(el.x2!), ty(el.y2!));
          ctx.stroke();
          break;

        case "dashed":
          ctx.setLineDash([el.dashLen || 5, el.dashLen || 5]);
          ctx.beginPath();
          ctx.moveTo(tx(el.x1!), ty(el.y1!));
          ctx.lineTo(tx(el.x2!), ty(el.y2!));
          ctx.stroke();
          ctx.setLineDash([]);
          break;

        case "rect":
          ctx.strokeRect(
            tx(el.x1!), ty(flipY ? el.y2! : el.y1!),
            (el.x2! - el.x1!) * scale, Math.abs(el.y2! - el.y1!) * scale
          );
          break;

        case "fillrect":
          ctx.fillStyle = el.fill || el.color || "#e0e0e0";
          ctx.fillRect(
            tx(el.x1!), ty(flipY ? el.y2! : el.y1!),
            (el.x2! - el.x1!) * scale, Math.abs(el.y2! - el.y1!) * scale
          );
          if (el.color && el.color !== el.fill) {
            ctx.strokeStyle = el.color;
            ctx.strokeRect(
              tx(el.x1!), ty(flipY ? el.y2! : el.y1!),
              (el.x2! - el.x1!) * scale, Math.abs(el.y2! - el.y1!) * scale
            );
          }
          break;

        case "circle":
          ctx.beginPath();
          ctx.arc(tx(el.x!), ty(el.y!), (el.r || 5) * scale, 0, Math.PI * 2);
          if (el.fill && el.fill !== "none") ctx.fill();
          ctx.stroke();
          break;

        case "ellipse":
          ctx.beginPath();
          ctx.ellipse(tx(el.x!), ty(el.y!), (el.rx || 5) * scale, (el.ry || 5) * scale, 0, 0, Math.PI * 2);
          if (el.fill && el.fill !== "none") ctx.fill();
          ctx.stroke();
          break;

        case "arc": {
          const sa = (el.startAngle || 0) * Math.PI / 180;
          const ea = (el.endAngle || 360) * Math.PI / 180;
          ctx.beginPath();
          if (flipY) {
            ctx.arc(tx(el.x!), ty(el.y!), (el.r || 5) * scale, -sa, -ea, true);
          } else {
            ctx.arc(tx(el.x!), ty(el.y!), (el.r || 5) * scale, sa, ea);
          }
          ctx.stroke();
          break;
        }

        case "arrow":
          drawArrow(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), color, lw, el.headSize || 10);
          break;

        case "darrow":
          drawArrow(ctx, tx(el.x1!), ty(el.y1!), tx(el.x2!), ty(el.y2!), color, lw, el.headSize || 8);
          drawArrow(ctx, tx(el.x2!), ty(el.y2!), tx(el.x1!), ty(el.y1!), color, lw, el.headSize || 8);
          break;

        case "polyline":
          if (el.points && el.points.length >= 4) {
            ctx.beginPath();
            ctx.moveTo(tx(el.points[0]), ty(el.points[1]));
            for (let i = 2; i < el.points.length - 1; i += 2) {
              ctx.lineTo(tx(el.points[i]), ty(el.points[i + 1]));
            }
            ctx.stroke();
          }
          break;

        case "polygon":
          if (el.points && el.points.length >= 6) {
            ctx.beginPath();
            ctx.moveTo(tx(el.points[0]), ty(el.points[1]));
            for (let i = 2; i < el.points.length - 1; i += 2) {
              ctx.lineTo(tx(el.points[i]), ty(el.points[i + 1]));
            }
            ctx.closePath();
            if (el.fill && el.fill !== "none") ctx.fill();
            ctx.stroke();
          }
          break;

        case "text": {
          const fs = el.fontSize || 11;
          const fw = el.fontWeight || "normal";
          ctx.font = `${fw} ${fs}px sans-serif`;
          ctx.fillStyle = color;
          ctx.textAlign = el.textAnchor || "middle";
          ctx.textBaseline = "middle";
          const txt = (el.text || "").replace(/_/g, " ");
          ctx.fillText(txt, tx(el.x!), ty(el.y!));
          break;
        }

        case "dim":
        case "hdim":
        case "vdim":
          drawDim(ctx, el, tx, ty, scale, flipY);
          break;

        case "hatch": {
          // Diagonal hatching inside a rect
          const hx1 = tx(el.x1!), hy1 = ty(flipY ? el.y2! : el.y1!);
          const hw = (el.x2! - el.x1!) * scale;
          const hh = Math.abs(el.y2! - el.y1!) * scale;
          ctx.save();
          ctx.beginPath();
          ctx.rect(hx1, hy1, hw, hh);
          ctx.clip();
          ctx.strokeStyle = color;
          ctx.lineWidth = 0.5;
          const step = 6;
          for (let d = -hh; d < hw + hh; d += step) {
            ctx.beginPath();
            ctx.moveTo(hx1 + d, hy1);
            ctx.lineTo(hx1 + d + hh, hy1 + hh);
            ctx.stroke();
          }
          ctx.restore();
          break;
        }
      }
    }
  }

  // ───── Pan & Zoom ─────
  let isDragging = false;
  let lastX = 0, lastY = 0;

  canvas.addEventListener("mousedown", (e) => {
    isDragging = true;
    lastX = e.clientX;
    lastY = e.clientY;
    canvas.style.cursor = "grabbing";
  });

  canvas.addEventListener("mousemove", (e) => {
    if (!isDragging) return;
    panX += e.clientX - lastX;
    panY += e.clientY - lastY;
    lastX = e.clientX;
    lastY = e.clientY;
    render();
  });

  canvas.addEventListener("mouseup", () => {
    isDragging = false;
    canvas.style.cursor = "grab";
  });

  canvas.addEventListener("mouseleave", () => {
    isDragging = false;
    canvas.style.cursor = "grab";
  });

  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
    const newScale = scale * factor;

    // Zoom towards mouse position
    panX = mx - (mx - panX) * (newScale / scale);
    panY = my - (my - panY) * (newScale / scale);
    scale = newScale;

    render();
  }, { passive: false });

  // Touch support
  let lastTouchDist = 0;
  canvas.addEventListener("touchstart", (e) => {
    if (e.touches.length === 1) {
      isDragging = true;
      lastX = e.touches[0].clientX;
      lastY = e.touches[0].clientY;
    } else if (e.touches.length === 2) {
      lastTouchDist = Math.hypot(
        e.touches[1].clientX - e.touches[0].clientX,
        e.touches[1].clientY - e.touches[0].clientY
      );
    }
  });

  canvas.addEventListener("touchmove", (e) => {
    e.preventDefault();
    if (e.touches.length === 1 && isDragging) {
      panX += e.touches[0].clientX - lastX;
      panY += e.touches[0].clientY - lastY;
      lastX = e.touches[0].clientX;
      lastY = e.touches[0].clientY;
      render();
    } else if (e.touches.length === 2) {
      const dist = Math.hypot(
        e.touches[1].clientX - e.touches[0].clientX,
        e.touches[1].clientY - e.touches[0].clientY
      );
      if (lastTouchDist > 0) {
        const factor = dist / lastTouchDist;
        const cx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        const cy = (e.touches[0].clientY + e.touches[1].clientY) / 2;
        const rect = canvas.getBoundingClientRect();
        const mx = cx - rect.left;
        const my = cy - rect.top;
        const newScale = scale * factor;
        panX = mx - (mx - panX) * (newScale / scale);
        panY = my - (my - panY) * (newScale / scale);
        scale = newScale;
        render();
      }
      lastTouchDist = dist;
    }
  }, { passive: false });

  canvas.addEventListener("touchend", () => {
    isDragging = false;
    lastTouchDist = 0;
  });

  // Double-click to reset view
  canvas.addEventListener("dblclick", () => {
    scale = Math.min((W - 2 * pad) / dx, (H - 2 * pad) / dy) * 0.9;
    panX = W / 2 - (xmin + xmax) / 2 * scale;
    panY = flipY
      ? H / 2 + (ymin + ymax) / 2 * scale
      : H / 2 - (ymin + ymax) / 2 * scale;
    render();
  });

  // Initial render
  render();
}

// ───── Helper functions ─────

function autoGridStep(range: number, scale: number): number {
  const targetPx = 50; // ~50px between grid lines
  const raw = targetPx / scale;
  const mag = Math.pow(10, Math.floor(Math.log10(raw)));
  const norm = raw / mag;
  if (norm < 2) return mag;
  if (norm < 5) return 2 * mag;
  return 5 * mag;
}

function drawArrow(
  ctx: CanvasRenderingContext2D,
  x1: number, y1: number, x2: number, y2: number,
  color: string, lw: number, headSize: number
) {
  const angle = Math.atan2(y2 - y1, x2 - x1);
  ctx.strokeStyle = color;
  ctx.lineWidth = lw;

  // Line
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();

  // Arrowhead
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.moveTo(x2, y2);
  ctx.lineTo(x2 - headSize * Math.cos(angle - 0.35), y2 - headSize * Math.sin(angle - 0.35));
  ctx.lineTo(x2 - headSize * Math.cos(angle + 0.35), y2 - headSize * Math.sin(angle + 0.35));
  ctx.closePath();
  ctx.fill();
}

function drawDim(
  ctx: CanvasRenderingContext2D,
  el: DrawElement,
  tx: (x: number) => number,
  ty: (y: number) => number,
  scale: number,
  flipY: boolean
) {
  const sx1 = tx(el.x1!), sy1 = ty(el.y1!);
  const sx2 = tx(el.x2!), sy2 = ty(el.y2!);
  const off = (el.offset || 0) * scale * (flipY ? -1 : 1);

  // Extension lines (dashed)
  ctx.setLineDash([2, 2]);
  ctx.strokeStyle = "#888";
  ctx.lineWidth = 0.8;

  if (el.type === "hdim" || el.type === "dim") {
    ctx.beginPath();
    ctx.moveTo(sx1, sy1); ctx.lineTo(sx1, sy1 + off + (off > 0 ? 5 : -5));
    ctx.moveTo(sx2, sy2); ctx.lineTo(sx2, sy2 + off + (off > 0 ? 5 : -5));
    ctx.stroke();
  }
  if (el.type === "vdim") {
    ctx.beginPath();
    ctx.moveTo(sx1, sy1); ctx.lineTo(sx1 + off + (off > 0 ? 5 : -5), sy1);
    ctx.moveTo(sx2, sy2); ctx.lineTo(sx2 + off + (off > 0 ? 5 : -5), sy2);
    ctx.stroke();
  }
  ctx.setLineDash([]);

  // Dimension line with arrows
  let dx1: number, dy1: number, dx2: number, dy2: number;
  if (el.type === "vdim") {
    dx1 = sx1 + off; dy1 = sy1;
    dx2 = sx2 + off; dy2 = sy2;
  } else {
    dx1 = sx1; dy1 = sy1 + off;
    dx2 = sx2; dy2 = sy2 + off;
  }

  ctx.strokeStyle = "#888";
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(dx1, dy1);
  ctx.lineTo(dx2, dy2);
  ctx.stroke();

  // Small arrowheads
  const angle = Math.atan2(dy2 - dy1, dx2 - dx1);
  const hs = 6;
  ctx.fillStyle = "#888";
  // Left arrow
  ctx.beginPath();
  ctx.moveTo(dx1, dy1);
  ctx.lineTo(dx1 + hs * Math.cos(angle - 0.4), dy1 + hs * Math.sin(angle - 0.4));
  ctx.lineTo(dx1 + hs * Math.cos(angle + 0.4), dy1 + hs * Math.sin(angle + 0.4));
  ctx.closePath(); ctx.fill();
  // Right arrow
  ctx.beginPath();
  ctx.moveTo(dx2, dy2);
  ctx.lineTo(dx2 - hs * Math.cos(angle - 0.4), dy2 - hs * Math.sin(angle - 0.4));
  ctx.lineTo(dx2 - hs * Math.cos(angle + 0.4), dy2 - hs * Math.sin(angle + 0.4));
  ctx.closePath(); ctx.fill();

  // Text
  if (el.text) {
    ctx.font = "11px sans-serif";
    ctx.fillStyle = "#555";
    ctx.textAlign = "center";
    ctx.textBaseline = "bottom";
    const txt = el.text.replace(/_/g, " ");
    ctx.fillText(txt, (dx1 + dx2) / 2, (dy1 + dy2) / 2 - 4);
  }
}
