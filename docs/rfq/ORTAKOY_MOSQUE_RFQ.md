# Request for Quotation – Ortaköy Mosque (Büyük Mecidiye Camii), real-time hero asset

> Interne Notiz (DE): Unverbindliche Angebotsanfrage, freigegeben am 2026-10-26. **Keine Auftragsvergabe ohne Freigabe des Product Owners.** Versand an 2–3 Artists/Studios mit Portfolio in historischer Architektur. Antworten in `docs/rfq/responses/` sammeln (ohne personenbezogene Daten im Repo).

## Project
- *Project Apex*, a formula racing game (Unity 6, URP; PC and mobile) with a street circuit along the Bosphorus in Istanbul.
- The mosque stands at the water's edge next to the circuit's turn 1 braking zone. It is seen from 30 to 600 m, at 60–330 km/h, and in replay cameras.
- This is a non-binding request for a quote. No order is placed by this document.

## Scope
- **Building:**
  - Ortaköy Mosque (Büyük Mecidiye Camii, Balyan family, 1854–56): exterior only, architecturally faithful.
  - Main dome, drum with windows, the two minarets with balconies, corner towers, façade ornament, window tracery, entrance.
- **Surroundings:**
  - The stone platform and quay wall to the water.
  - The paved forecourt on the land side, including the low wall and railings.
- **Interior:** Hinted only through windows (dark interior card or simple parallax).
- **Out of scope:** Interior, animated people, vehicles.

## Technical requirements

| Item | Requirement |
|---|---|
| Units / axes | metres, +Y up, +Z along the qibla axis (approx. 151° true), origin at the centre of the building's footprint at ground level |
| LOD0 | 120–180k triangles, ornament mainly via normal maps and decals rather than geometry |
| LOD1 / LOD2 / LOD3 | ~40k / ~10k / ~2k, silhouette-preserving (dome, minarets), no visible popping with cross-fade |
| Impostor | octahedral or billboard impostor for > 1.5 km (or agreed alternative) |
| Materials | URP Lit, metallic/smoothness: albedo, normal, mask (metallic, AO, detail mask, smoothness) |
| Textures | 2 tiling trim sheets 4K (marble/limestone, ornament) + 1 unique atlas 4K; mobile set at 2K; texel density ~512 px/m for close surfaces |
| Lightmap | non-overlapping UV2 or suitability for Unity Adaptive Probe Volumes |
| Collision | simple convex hulls (no mesh collider from LOD0) |
| Delivery | FBX + source files (Blender or Max), textures as PNG/TGA 16-bit where needed, a Unity package with prefab and LODGroup |
| Night | emissive mask for the floodlit façade (optional, quoted separately) |

## Accuracy and references
- Accuracy: Proportions faithful to the real building. Dome, minarets and façade bays must be countable and in the right place.
- References from the artist: own photos, or images with a commercial licence.
- Forbidden sources:
  - No Google Maps/Earth/Street View data or geometry.
  - No scan data or models from other games.
  - No unlicensed photos used as textures.
- Respect: The building is a place of worship. No branding or advertising on it, and no destruction states.

## Rights
- Delivery under a written licence or transfer of rights that allows unlimited commercial use in the game (all platforms), modification, and distribution in compiled form.
- The artist confirms that all content is their own work or properly licensed. They list every third-party component, such as texture sources with licences.
- No use of the delivered asset for AI training by either party without consent.

## Milestones (suggested)
1. Blockout with dimensions and screenshots from given camera points (go/no-go)
2. High poly / LOD0 untextured
3. Textured LOD0 in Unity URP under a golden-hour HDRI
4. LODs, impostor, optimisation, final delivery

## Please quote
- Price per milestone (fixed price), currency, VAT status
- Timeline per milestone
- Number of revision rounds included
- 3–5 portfolio examples of real-time historical or religious architecture (with polycount and engine)
- Software and pipeline

## Contact
- Via the project owner (contact details are added at sending time and are not stored in the repository).
