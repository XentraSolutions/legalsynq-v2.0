const EARTH_RADIUS_MILES = 3958.7613;

/**
 * Points tracing a geographic circle of `radiusMiles` around (lat, lng).
 * Google Maps' `Circle` overlay has no dashed-stroke option, so a dashed coverage
 * ring is drawn as a `Polyline` along this path instead, using the icons/repeat trick.
 */
export function circleOutlinePoints(
  lat: number,
  lng: number,
  radiusMiles: number,
  steps = 72,
): google.maps.LatLngLiteral[] {
  const latRad = (lat * Math.PI) / 180;
  const angularDistance = radiusMiles / EARTH_RADIUS_MILES;
  const points: google.maps.LatLngLiteral[] = [];

  for (let i = 0; i <= steps; i++) {
    const bearing = (i / steps) * 2 * Math.PI;
    const pointLatRad = Math.asin(
      Math.sin(latRad) * Math.cos(angularDistance) +
      Math.cos(latRad) * Math.sin(angularDistance) * Math.cos(bearing),
    );
    const pointLngRad =
      (lng * Math.PI) / 180 +
      Math.atan2(
        Math.sin(bearing) * Math.sin(angularDistance) * Math.cos(latRad),
        Math.cos(angularDistance) - Math.sin(latRad) * Math.sin(pointLatRad),
      );
    points.push({ lat: (pointLatRad * 180) / Math.PI, lng: (pointLngRad * 180) / Math.PI });
  }

  return points;
}

/** Dashed-line icon config for a coverage-radius Polyline (Google Maps has no native dashArray). */
export const DASHED_RING_ICONS: google.maps.IconSequence[] = [
  {
    icon: { path: 'M 0,-1 0,1', strokeOpacity: 1, scale: 3 },
    offset: '0',
    repeat: '14px',
  },
];
