/* Bosphorus GP core area: streets, coastline, buildings, parks, landmarks.
   Run on https://overpass-turbo.eu (Export -> "raw OSM data") or POST to
   https://overpass-api.de/api/interpreter. Save as data/geo/osm/bosphorus_core.osm
   Data © OpenStreetMap contributors, ODbL 1.0. */
[out:xml][timeout:300][bbox:41.020,28.970,41.065,29.050];
(
  way["highway"];
  way["natural"="coastline"];
  way["building"];
  relation["building"];
  way["building:part"];
  way["leisure"~"park|garden|stadium|pitch"];
  way["landuse"~"grass|forest|recreation_ground|cemetery"];
  way["natural"~"wood|water|scrub"];
  way["man_made"~"bridge|pier"];
  way["barrier"="wall"];
  node["amenity"="place_of_worship"];
  way["amenity"="place_of_worship"];
  node["historic"]; way["historic"];
  node["tourism"~"attraction|museum|viewpoint"]; way["tourism"~"attraction|museum"];
  node["highway"~"traffic_signals|street_lamp"];
);
(._;>;);
out body;
