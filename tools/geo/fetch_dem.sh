#!/bin/sh
# Downloads the Copernicus DEM GLO-30 tiles for Istanbul (N41 E028, N41 E029) into data/geo/dem/.
# Licence: Copernicus WorldDEM-30 (free, attribution required, see docs/ASSET_LICENSES.md).
set -e
cd "$(dirname "$0")/../../data/geo/dem"
for t in N41_00_E028_00 N41_00_E029_00; do
  [ -f "cop_$t.tif" ] || curl -sSf -o "cop_$t.tif" "https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_${t}_DEM/Copernicus_DSM_COG_10_${t}_DEM.tif"
done
ls -la
