/* Wider area: coastline and water only (skyline layer, Golden Horn, historic peninsula, Asian shore).
   Save as data/geo/osm/bosphorus_coast.osm. Data © OpenStreetMap contributors, ODbL 1.0. */
[out:xml][timeout:300][bbox:40.990,28.930,41.100,29.100];
(
  way["natural"="coastline"];
  way["natural"="water"];
  relation["natural"="water"];
  way["man_made"~"bridge|pier"];
);
(._;>;);
out body;
