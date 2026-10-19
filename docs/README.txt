ZEUS - WORLD MAPS TOOLS 1.0 (World Map Converter, World Map Generator) - Zeus: Master of Olympus / Poseidon
========================================================================================================
(Version française : LISEZMOI.txt)

What it is for
--------------
At widescreen resolutions, the game shows the world map in a small corner of the screen.
With full-screen world maps, the cities, region names and trade routes of every scenario
must also be moved onto the enlarged map. This program does it, whatever the world map
size, on:
  - scenarios (.pak, .set),
  - adventure editor maps (.map),
  - saved games (.sav: a saved game keeps its own copy of the world map, including the
    one used for the next episodes of an adventure).

World maps
----------
No picture is provided: "World Map Generator.exe" makes the 14 world maps on your
computer, from the original maps of YOUR game, for any resolution. The map is always
centred:
  - Greece (the 10 Zeus maps) sits between Italy on the left and Anatolia, the Black Sea,
    Cyprus and the Levant on the right. These lands are drawn once on a "3440 x 1440"
    reference (3238 x 1410 view), their outline taken from Poseidon_map04 and their
    relief from the land of the map of Greece; every smaller resolution is a centred crop
    of that reference (taller views get a reference redrawn at their height). Without
    Poseidon, Greece is surrounded by textured water.
  - The drawings in the bottom-left corner of maps 01 to 07 are moved to the bottom-left
    corner of the enlarged world map; their old place shows the sea of the map without
    a drawing.
  - Poseidon maps: each one is drawn on a "1440p" reference (2338 x 1410 view). Maps 01,
    02 and 03 are the same painting of the world: around each map, the real pixels of
    the neighbouring maps. Where no map is painted (west of map03, east of map02, both
    sides of map04), the real coastlines (Natural Earth, public domain) fitted to the
    painting, with the relief, water and coast of the map itself; no lakes. Smaller
    resolutions are centred crops of the reference. On wider screens (ultrawide), the
    reference stays in the centre and the rest gets the Atlantis panel, textured water or
    a custom picture, with or without a Greek key border.
The "From a Zeus.exe…" button reads the resolution from the exe. The originals are kept
in "Mappemondes d'origine" and can be put back.

Using the converter
-------------------
1. Run "World Map Converter.exe" (nothing to install).
2. First choose the game's Zeus.exe: the program shows its version (English 2.1.4.0 or
   French), its resolution and the state of its scenario editor, and suggests the world
   map size: the one installed next to the exe (DATA), otherwise the view size from the
   resolution. Change it if needed (1738 x 1080, 2376 x 1410, 3238 x 1410 or "Other size");
   the map is centred, as in the generator.
3. Leave "Patch this exe's scenario editor for this world map" ticked: the editor will
   place cities in the original zone of the map, enlarged with it, and never outside the
   original map (English or French exe, any size). A copy
   of the original exe is kept (Zeus.exe.bak).
4. Drag your files (or the game's Adventures and Save folders) into the window, or use
   "Add files…" / "Add a folder…".
5. Choose "Replace the files" (a copy of the original is kept next to it, as ".bak") or
   "Save the converted files in this folder".
6. Click "Convert": the editor is patched if needed, then the files are converted.

"Restore the originals" puts the .bak files back. The "Language" list at the top right
switches the program to French.

Good to know
------------
- A file already converted for the same world map is recognised and left alone: it is
  never enlarged twice.
- Changing size or position is possible: from the original .bak when it is there (same
  result as a direct conversion), otherwise directly (one pixel off at most).
- Before writing anything, the program checks that it only changes world map positions.
  If in doubt, the file is not touched.
- For a game in progress, also convert its saves (.sav). An adventure save holds the
  world map of the next episodes: it is converted too. A save damaged by an earlier
  version (cities enlarged but routes back to the original after an episode change) is
  repaired.
- To play an English game in French, see Zeus - EXE French Patch
  (https://github.com/Vicinity4813/zeus-exe-french-patch).
- The program never connects to the Internet and only changes the files you give it.
  Its source code is in the "source" folder.

Credits
-------
- Reading .pak / .set files (PKWare DCL decompression, map structure) is adapted from
  zeus_mapper by zawapl (Jakub Zawadzki), MIT licence: see source\LICENSE-zeus_mapper.txt.
- Coastlines around the Poseidon maps: Natural Earth (naturalearthdata.com), public domain.
- Zeus: Master of Olympus / Poseidon: Impressions Games / Sierra.

Command line
------------
"World Map Converter.exe" [--resolution 1080|1440|3440|WxH] [--taille WxH] [--exe Zeus.exe [--editeur]]
                               [--sortie FOLDER] files...
"World Map Generator.exe" --jeu FOLDER (--resolution WxH | --taille WxH) [--fond panneau|eau|image --image FICHIER] [--sans-frise] | --restaurer
