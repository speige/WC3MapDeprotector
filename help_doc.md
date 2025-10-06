# Help Documentation
### WC3 Map Deprotector

**Last Updated:** 10/5/2025

## Table of Contents
*(Click a section to jump to it)*

1. [Bug Testing](#1-bug-testing)
2. [Editor Crashes](#2-editor-crashes)
3. [Jass Triggers](#3-jass-triggers)
   - 3.1 [Jass Compiler](#31-jass-compiler-new)
   - 3.2 [Fixing Errors](#32-fixing-errors-new)
4. [Unknown Files](#4-unknown-files)
   - 4.1 [Merging Listfiles – Find more unknown files](#41-merging-listfiles--find-more-unknown-files-new)
5. [Duplicate Units](#5-duplicate-units)
6. [Object Manager – Missing units or wrong attributes](#6-object-manager--missing-units-or-wrong-attributes)
7. [Campaigns](#7-campaigns)

---

## 1. Bug Testing

Deprotection is not perfect. Please thoroughly test the map and manually correct any issues that are found. The most common bugs are:

- Compiler errors in JASS Triggers
- Duplicate Units/etc (see above)
- Missing pre-placed objects [Units/Items/Doodads/Cameras/Regions] in the Object Manager module
- Wrong values on pre-placed objects (Item Droptables, etc.)
- Incorrect Object Editor data

## 2. Editor Crashes

World Editor crashes frequently in HD mode, but works much better in SD mode. You can still play the game in HD mode even if the editor is in SD mode.

You can change this in the editor by clicking **File/Preferences** and choosing **SD** under "Asset Mode". This will not affect map gameplay; only the editor will be affected.

<img width="484" height="645" alt="img_0" src="https://github.com/user-attachments/assets/6168d4b2-12c9-4150-ae93-c126dd66626d" />

## 3. Jass Triggers

The deprotected JASS will be in plain text coding format. You can see it by clicking on the root icon of the trigger editor.

<img width="1157" height="480" alt="img_1" src="https://github.com/user-attachments/assets/3c23c14e-beba-43b4-8b8b-d359e2272804" />

### 3.1 Jass Compiler

Before attempting to modify the JASS code, ensure that JassHelper is enabled. JassHelper is a vital compiler that helps identify errors and makes debugging easier.

<img width="211" height="260" alt="img_2" src="https://github.com/user-attachments/assets/a9677d38-0cc8-4410-869f-66f1be1c620a" />

### 3.2 Fixing Errors

If there are JASS errors, simply copy the error message and code output by JassHelper and paste it into an LLM, such as ChatGPT, for a solution. Then, in the "Custom Script Code" section, replace the affected code with your fix. You may need to repeat this process several times. It may be helpful to work in a text editor.

<img width="1281" height="923" alt="img_3" src="https://github.com/user-attachments/assets/34fdd85f-1a14-44a1-ac6a-2d1ad758ab6e" />

## 4. Unknown Files

The MPQ file format converts all filenames to numbers (called hashes) for quicker loading. The world editor still needs the real file name, but the game only needs the hash. Protectors delete the filenames, leaving only the hashes. This is what's called an "unknown" file. These are typically 3D models, textures, or sounds. The deprotector attempts to recover the original file names, but it's not 100% successful for all files.

When a name cannot be recovered, it may mean the actual file is not in use. For example, maybe the author added a 3D model but never assigned it to a unit. The only way to verify this is by testing the map for bugs after deprotection.

> **Note:** "Unknown" does not mean the file is missing or corrupt; it means we don't know the correct file name. It can still be opened in an image editor.

An "unknown" 3D model will display in the world editor as a **green box**. An "unknown" texture will display in the world editor as a unit that's **completely white**. The purple boxes are pathing blockers and can be ignored.

The deprotector utilizes all known automated methods to recover the file names; any remaining "unknowns" must be recovered manually.

<img width="1147" height="668" alt="img_4" src="https://github.com/user-attachments/assets/46aca06a-c553-495a-899e-19a299e655b6" />

### Manual Recovery Process

To view the "unknown" files, you need to use MPQEditor to export the (listfile) from the deprotected map and import it into the original map. Any remaining `File00000###.blp` (or mdx, wav, mp3, etc) files are the "Unknowns".

To fix them:

1. Extract them in a viewer program, such as [Hive's "Model Checker"](https://www.hiveworkshop.com/pages/model-checker)
2. Pick a new name for the file and add it using the Module/Asset Manager in World Editor
3. Locate all locations in the World Editor where the asset should be referenced and select the new file name
   - Usually found in the Object Editor under attributes such as "Art – Icon" or "Art – Model"
   - For sounds, it may be in the Jass
4. Open the MDX file in a model editing program and correct all the texture references to use the new name

### Finding Correct Names

If you can discover the correct name, it will save you the step of correcting all the asset references. A possible way to do this is by using [Hive's "Asset Scanner"](https://www.hiveworkshop.com/asset-scanner). Even though the "unknown" file has the wrong name, the scanner can still find other maps containing the identical file because it searches the actual data in the file, not the name.

### 4.1 Merging Listfiles – Find more unknown files

Warcraft 3 maps utilize a file called "Listfiles" to identify the names of all components and assets within a map. The world editor requires these names to properly view and interact with data. If too many files cannot be identified, it makes deprotection difficult.

WC3 Map Deprotector includes a fairly comprehensive listfile saved at this location:
```
C:\Users\Your Username\AppData\Roaming\WC3MapDeprotector
```

The filenames included cover some of the more common map types, such as Anime RPGs and custom games. However, it is not fully complete, as maps are constantly being updated with new assets/filenames.

<img width="894" height="614" alt="img_5" src="https://github.com/user-attachments/assets/14518eca-bed0-47c3-948c-527988cc54c6" />

#### Listfile Merging Process

One technique that can be applied is listfile merging. It's essentially rebuilding a listfile specially tailored for the map you want to deprotect.

1. Download **MPQ Editor** (Available on MPQ Archive through Google search)
2. Open the MPQ of the map you want to deprotect
3. Go to **Tools** → **W3X Name Scanner**
4. Hit **Scan**, then **Merge Listfiles**
5. Point to where the listfile in the WC3 Map Deprotector is stored
6. Re-run the deprotection tool

<img width="638" height="650" alt="img_6" src="https://github.com/user-attachments/assets/68a41ae8-f63b-4fff-b282-d2caf183b46c" />

By combining the listfile scanners from WC3 Map Deprotector and MPQ Editor, you can identify more files, making them much more manageable to work with.

<img width="429" height="410" alt="img_7" src="https://github.com/user-attachments/assets/e93f3463-d858-48fc-bb9e-daea4bc2eb30" />

## 5. Duplicate Units

<img width="603" height="582" alt="img_8" src="https://github.com/user-attachments/assets/9d6dfbaf-eeae-4bf6-9692-b62aae322b00" />

The world editor tracks units placed in the render window inside the W3X archive with a file named `War3MapUnits.doo`. Upon saving, these are converted into a function in the `War3Map.j` file called `CreateAllUnits`.

Protection deletes the `war3mapunits.doo` file and de-protection attempts to recover it. If successful, it comments out the original `CreateAllUnits`. However, if protection also scrambled the code inside `CreateAllUnits`, de-protection might fail to comment out the correct lines of code.

### Fix Process

To fix this, you must search the triggers in the world editor for where these duplicate units are created and comment them out manually:
- Start by looking at `main_old` and read the code & function calls from there
- This can also happen with items, cameras, sounds, and regions

## 6. Object Manager – Missing units or wrong attributes

There are auto-generated native functions such as `CreateUnitsForPlayer1`. This is the Jass version of the objects displayed in the render window (shown in Object Manager).

Protection empties the "Object Manager" and obfuscates the Jass by scrambling the code (renaming `CreateUnitsForPlayer1` to random characters, such as `NXE`). When de-protection can decipher the code, it adds them back to the "Object Manager" & render window. If it fails to decipher the code, units will be lost or their attributes will be wrong.

To help you determine what was deciphered and what was lost, it comments out the portion of the Jass that was deciphered correctly (since it will be re-generated by "World Editor" on save).

<img width="732" height="315" alt="img_9" src="https://github.com/user-attachments/assets/44798959-959f-4484-962b-8aacaaa3fb57" />

<img width="1397" height="1259" alt="img_10" src="https://github.com/user-attachments/assets/fca47b97-22b3-4c4b-9dc9-808f74684cca" />

<img width="1406" height="1074" alt="img_11" src="https://github.com/user-attachments/assets/194e8572-ccd5-4400-a3c9-c071e6efc999" />

### Example Issues

- **Missing Units:** A unit like "War Room" with FourCC `'h995'` might be shown in-game on the protected map but missing from the Object Manager in the World Editor
- **Incorrect Attributes:** A unit might be created with default values instead of custom ones (e.g., Mana left at default instead of being reset to 0)

<img width="610" height="539" alt="img_12" src="https://github.com/user-attachments/assets/f11e8fa2-d477-4fbc-adab-be3cddcb12f0" />

### Troubleshooting

You can resolve these issues by:
1. Manually re-adding the objects to the render window
2. Reading the code for clues about what attributes to set on the unit
3. Using ChatGPT to explain the Jass if you don't understand it

### Identifying Failed Deciphering

You will know that deciphering the JASS into ObjectManager failed if, after saving in World Editor, you test the map and find bugs. Use the **WinMerge** app to find clues:

1. Open the deprotected map in MPQEditor
2. Extract `war3map.j`
3. Compare it against the World Editor triggers
4. Look for sections where part of the code is commented out and part isn't

> **Important:** Remember that Object Manager attributes are tied to a specific instance of a unit placed in the render window; this is different from the "Object Editor" where you set default values for all units of that type.

<img width="1288" height="323" alt="img_13" src="https://github.com/user-attachments/assets/0f84a290-d570-4f28-b3c0-486dfaa7ebf5" />

## 7. Campaigns

Campaign files have the `.w3n` file extension. This is still the same MPQ format that all WC3 maps use; however, it shares resources (3D models/textures/etc) in the base archive, and each mission is embedded as a separate W3X inside the W3N.

### Deprotecting Campaigns

This tool only supports deprotecting `.w3x` files, but there is an easy workaround:

1. Download [Xetanth87's Campaign Splitter](https://www.hiveworkshop.com/threads/xetanth87s-campaign-splitter-turn-custom-campaigns-into-separate-maps-now-with-archon-mode.340521)
2. Open a command prompt and run:
   ```bash
   java -jar XT87CampaignSplitterGUI.jar
   ```
3. After your campaign is split into separate files, run the deprotector on each `.w3x` file separately

---
© 2025 WC3 Map Deprotector Team • Licensed under MIT
