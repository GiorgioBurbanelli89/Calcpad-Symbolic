import { fem2d } from "./fem2d";
import { fem3d } from "./fem3d";
import { chart } from "./chart";
import { frameViewer } from "./frameViewer";
import { structDraw } from "./structDraw";
import { draw } from "./draw";

export { fem2d, fem3d, chart, frameViewer, structDraw, draw };

const CalcpadViz = { fem2d, fem3d, chart, frameViewer, structDraw, draw };

if (typeof window !== "undefined") {
  (window as any).CalcpadViz = CalcpadViz;
}

export default CalcpadViz;
