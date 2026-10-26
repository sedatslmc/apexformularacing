# Request for Quotation – Ortaköy / Bosphorus façade kit (modular, real-time)

> Interne Notiz (DE): Unverbindliche Angebotsanfrage, freigegeben am 2026-10-26. **Keine Auftragsvergabe ohne Freigabe.** Kann an dieselben Artists wie die Moschee-Anfrage gehen; getrennt anbieten lassen.

## Project
- *Project Apex* (Unity 6, URP; PC and mobile) is a street circuit along the Bosphorus.
- The first premium section is around the Ortaköy square and the shore road at turn 1.
- Buildings are seen from the track at 10–300 m and 60–330 km/h.
- This is a non-binding request for a quote.

## Scope
- **Modules:** A modular kit of ~40 pieces in the architectural language of 19th/early-20th-century Istanbul shore districts (Ortaköy, Beşiktaş):
  - ground floors with shop fronts and shutters
  - window bays (several widths), bay windows (cumba)
  - cornices, string courses, corner pieces
  - roofs with tiles, chimneys, balconies with railings
  - doors, awnings
- **Buildings:** 6–8 assembled buildings as prefabs from the kit, roughly matching the buildings around the Ortaköy square and the shore road.
  - We provide footprints from OpenStreetMap and our circuit coordinates.
- **Variation:** Colour and dirt variants via masks (plaster colours typical for Istanbul), plus weathering decals.

## Technical requirements

| Item | Requirement |
|---|---|
| Units / grid | metres, +Y up, modules on a 0.5 m grid, pivots at the bottom-left of the module |
| Budget | assembled building LOD0 10–25k triangles; LOD1 ~30 %, LOD2 ~8 %; far LOD as merged block with baked façade texture |
| Materials | URP Lit; 3 tiling trim sheets 4K (plaster/stone, wood/shutters, roof/metal) + mobile 2K; masks for colour variation |
| Glass | interior mapping or dark interior cards (no transparency sorting issues) |
| Draw calls | kit shares materials; static batching/GPU instancing friendly |
| Delivery | FBX + sources, Unity package with prefabs, LODGroups and a short assembly guide |

## Accuracy, references, rights
Same conditions as the mosque RFQ:
- Own or licensed references; no Google data, no assets from other games.
- Full commercial licence for the game, modification and distribution.
- List all third-party components; no AI-training use without consent.

## Please quote
- Kit (modules and trim sheets)
- Each assembled building
- Optional far-LOD baking
- Timeline, revision rounds, and portfolio of modular real-time architecture
