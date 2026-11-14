import { lazy } from "react";

// The map is the only page that needs the WebGL renderer and the force simulation (Pixi + d3, the largest
// libraries in the app), so its code loads on demand instead of with the first page.
const SemanticMapView = lazy(() =>
  import("../../features/semantic-map").then((module) => ({ default: module.SemanticMapView })),
);

export function SemanticMapPage() {
  return <SemanticMapView />;
}
