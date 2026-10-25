# Fahrzeug-Inspektionsrenders (ohne Unity)

PBR-Renders des prozeduralen APEX A01 mit three.js im vorinstallierten Headless-Chromium.

```sh
cd build/Apex.ModelPreview && dotnet run -c Release -- /tmp/model     # OBJ je LOD exportieren
cd tools/carrender && npm install three@0.160.0                       # einmalig
node render.mjs /tmp/model/car_lod0.obj /tmp/a01                       # alle Ansichten
python3 contact_sheet.py /tmp/a01 /tmp/sheet.png side front rear top front34 rear34
```

Die Materialwerte in `render.html` entsprechen den URP-Materialien in `ApexMaterials`.
Bei Unity-Lit sehen die Bilder nur ähnlich aus, nicht identisch. Verbindlich ist die Darstellung im Editor.
`node_modules/` wird nicht eingecheckt.
