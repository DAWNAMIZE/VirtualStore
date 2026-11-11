# Virtual Store — Nighthawks Video, Aisle 03

A first-person, walkable late-night video rental store aisle built in **Unity 6 (URP)** and
targeting **WebGL** for desktop and mobile browsers.

Everything in the scene is generated procedurally by an editor script, so the aisle, shelves,
movie cases, posters and signage are reproducible and share a small set of materials.

---

## Requirements

| | |
|---|---|
| Unity | 6000.3.10f1 (Unity 6.3) |
| Render pipeline | Universal Render Pipeline 17.3.0 |
| Input | Input System package 1.18.0 |
| Build target | WebGL (desktop + mobile browsers) |

---

## Opening the project

1. Open the project folder in Unity Hub.
2. Open `Assets/Scenes/StoreAisle.unity` (it is already the only scene in Build Settings).
3. Press **Play**.

If the scene is ever missing or you want to regenerate it from scratch:

> **Tools ▸ Virtual Store ▸ Build Store Scene**

This rebuilds textures, materials, geometry, lighting, post-processing and the player, then
saves `Assets/Scenes/StoreAisle.unity` and registers it in Build Settings.

---

## Controls

**Desktop / laptop**

| Action | Input |
|---|---|
| Move | `W` `A` `S` `D` or arrow keys |
| Look | Mouse |
| Run | `Shift` |
| Capture cursor | Click |
| Release cursor | `Esc` |

**Mobile / touch**

| Action | Input |
|---|---|
| Move | On-screen joystick (bottom-left) |
| Look | Drag anywhere else on the screen |
| Run | Push the joystick to its outer edge |

The joystick is shown automatically on touch-capable devices. Touch support is detected
explicitly because `Application.isMobilePlatform` returns `false` inside WebGL, even on phones.

---

## Interaction

Walk up to a shelf and look at a case. A crosshair ring fills in, the case gains an emissive
highlight, and a panel appears with its placeholder title, genre and year.

- Raycast range: 3.6 m (`StoreInteraction.distance`)
- Dwell before highlight: 0.08 s
- HUD fade: handled by `VirtualStoreHUD`

---

## Building for WebGL

> **Tools ▸ Virtual Store ▸ Build WebGL**

Output goes to `Builds/WebGL/`. The menu item also configures:

- 1280 × 720 default canvas
- Brotli compression **with decompression fallback** (works on any plain static server)
- 256 MB initial WebGL memory
- Data caching enabled
- Run in background enabled

With decompression fallback enabled, Unity emits `.unityweb` payloads and a self-contained
loader, so no server-side `Content-Encoding` configuration is required.

### Running the build

Browsers will not load WebGL builds from `file://`. Serve the folder over HTTP:

```powershell
# from the repository root
python -m http.server 8080 --directory Builds/WebGL
```

Then open <http://localhost:8080>.

Any static host works (itch.io, Netlify, GitHub Pages, …).

### Reference renders

`Docs/Screenshots/` contains 1280 × 720 captures of the finished scene:

| File | View |
|---|---|
| `01-aisle-start.png` | Player start, looking down the aisle |
| `02-aisle-mid.png` | Mid-aisle |
| `03-shelf-closeup.png` | Close-up of a stocked shelf |
| `04-back-wall.png` | Back wall posters and neon sign |

`Builds/` is ignored by git; regenerate it with the menu item above.

---

## Project structure

```
Assets/
  Editor/
    VirtualStoreBuilder.cs     Procedural scene, textures, materials, lighting, WebGL menu
  Scenes/
    StoreAisle.unity           Generated scene (1,230 interactive cases)
  Scripts/
    FirstPersonController.cs   Desktop + touch movement/look, head bob, cursor handling
    MobileJoystick.cs          Fixed on-screen joystick (pointer events)
    VirtualStoreHUD.cs         Runtime uGUI: crosshair, focus prompt, hints, start overlay
    StoreInteraction.cs        Centre-screen raycast, case highlight, HUD feed
    MovieCase.cs               Per-case data (title, year, genre, accent)
    StoreAmbience.cs           Procedural fluorescent hum
  Materials/                   27 shared materials
  Textures/                    Generated checker floor, case spine, 5 posters
  Settings/
    StoreVolumeProfile.asset   Bloom, ACES, colour adjustments, vignette
```

### Scene layout

| Element | Value |
|---|---|
| Aisle length | 24 m (room 28 m) |
| Aisle width | 3.24 m between shelf fronts |
| Floor tiles | 1.2 m black/white checker |
| Shelf levels | 4 (0.42 / 0.88 / 1.34 / 1.80 m) |
| Hanging signs | CAUTION, NO HOPE, NIGHTHAWKS, LONG SHOT |
| Back wall | 5 posters + neon "NIGHTHAWKS VIDEO" |
| Ceiling lights | 5 warm point lights + 1 directional key |
| Player start | `(0, 0.05, -12.6)`, facing down the aisle |

---

## Performance notes (WebGL)

- **Shared materials only.** 8 case materials, 5 poster materials and 13 architectural
  materials cover the entire scene.
- **Static batching.** Every generated box is marked static; ~1,230 cases collapse into a
  handful of draw batches.
- **No realtime shadows on point lights.** Only the directional key light casts soft shadows.
- **Procedural textures** are generated once at build time and imported as compressed assets.
- Highlighting swaps one renderer's material to a cached emissive copy, so at most one extra
  material instance exists at a time.

---

## Customising

| What | Where |
|---|---|
| Movie titles | `VirtualStoreBuilder.Titles` |
| Genres | `VirtualStoreBuilder.Genres` |
| Poster names / art | `PosterNames` + `CreatePoster()` |
| Case spine colours | `caseColors` in `BuildMaterials()` |
| Aisle dimensions | `FloorLength`, `AisleLength`, `ShelfFrontX`, `CeilingHeight` |
| Lighting / exposure | `BuildLighting()`, `BuildPostProcessing()`, `ConfigureLighting()` |
| Walk speed / sensitivity | `FirstPersonController` inspector fields |

After editing any builder value, run **Tools ▸ Virtual Store ▸ Build Store Scene** again.

---

## Known limitations

- Titles are placeholder strings; there is no real box art on the cases (spine texture only).
- The player is constrained to the aisle by the shelf colliders; there is no other room.
- Mobile performance depends on the device GPU; reduce `Bloom` intensity or disable
  post-processing in `BuildPostProcessing()` for very low-end phones.
