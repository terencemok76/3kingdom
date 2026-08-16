import fs from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const scenePath = path.join(root, 'scenes', 'battle', 'field', 'FieldBattleLuoyang.tscn');
const width = 25;
const height = 25;

function encodeCells(cells) {
    const buffer = Buffer.alloc(cells.length * 12 + 2);
    cells.sort((left, right) => left.y - right.y || left.x - right.x).forEach((cell, index) => {
        const offset = index * 12;
        buffer.writeUInt32LE(cell.x << 16, offset);
        buffer.writeUInt32LE(cell.y | ((cell.source ?? 0) << 16), offset + 4);
        buffer.writeUInt32LE(cell.tile, offset + 8);
    });
    return buffer.toString('base64');
}

function buildGround() {
    const cells = Array.from({ length: width * height }, (_, index) => ({
        x: index % width,
        y: Math.floor(index / width),
        tile: 0
    }));
    const set = (x, y, tile) => { cells[y * width + x].tile = tile; };

    // Mangshan foothills north of the Luoyang river plain.
    for (let y = 0; y <= 3; y++) for (let x = 0; x < width; x++) set(x, y, 4);
    for (let y = 4; y <= 6; y++) {
        for (let x = 0; x < width; x++) {
            if (x <= 5 || x >= 18 || (y === 4 && x >= 9 && x <= 12)) set(x, y, 4);
        }
    }

    // The Luo River runs east-west across the basin, with wet banks and a ford road.
    for (let x = 0; x < width; x++) {
        const riverY = x < 6 || x > 18 ? 13 : 14;
        set(x, riverY, 6);
        set(x, Math.min(height - 1, riverY + 1), 6);
        const northBankTile = x >= 11 && x <= 13
            ? 0x10003 // Pebble at the ford approach.
            : x % 5 === 0
                ? 0x10002 // Mud in the lower wet pockets.
                : 0x10001; // Wet Grass along the river terrace.
        set(x, riverY - 1, northBankTile);
        set(x, Math.min(height - 1, riverY + 2), x >= 11 && x <= 13 ? 0x10003 : 0x10004); // Pebble ford or Shallow Water.
    }

    for (const x of [11, 12, 13]) for (let y = 12; y <= 16; y++) set(x, y, 1);
    // Pebble shoulders preserve a visible gravel landing around the wider ford road.
    for (const x of [10, 14]) {
        const riverY = 14;
        set(x, riverY - 1, 0x10003);
        set(x, riverY + 2, 0x10003);
    }
    for (let y = 5; y < height; y++) {
        const x = Math.min(width - 1, 6 + Math.floor((y - 5) * 0.45));
        set(x, y, 1);
        if (y > 16) set(Math.min(width - 1, x + 1), y, 1);
    }
    for (let x = 15; x <= 23; x++) set(x, 8, 1);
    return cells;
}

function buildObjects() {
    const cells = [];
    const forestCanopies = [[1, 1, 0], [5, 2, 1], [9, 1, 2], [13, 2, 3], [17, 1, 0x10000], [21, 2, 0x10001], [3, 4, 0x10002], [19, 4, 0x10003],
        [4, 2, 0], [8, 3, 1], [11, 3, 2], [16, 2, 3], [20, 3, 0x10000], [23, 4, 0x10001], [2, 5, 0x10002], [18, 5, 0x10003]];
    const woodCanopies = [[2, 2, 0], [6, 3, 1], [10, 2, 2], [14, 3, 3], [18, 2, 0x10000], [22, 3, 0x10001], [1, 5, 0x10002], [21, 5, 0x10003],
        [5, 4, 0], [9, 4, 1], [13, 4, 2], [17, 3, 3], [20, 5, 0x10000], [24, 3, 0x10001], [6, 5, 0x10002], [15, 5, 0x10003]];
    const hillTiles = [[3, 1, 0], [7, 2, 1], [11, 1, 2], [15, 1, 3], [4, 5, 0x10000], [8, 4, 0x10001], [12, 5, 0x10002], [16, 4, 0x10003],
        [0, 4, 0], [7, 5, 1], [14, 5, 2], [22, 5, 3]];
    const mountainTiles = [
        ...Array.from({ length: width }, (_, x) => [x, 0, x % 4]),
        [0, 1, 0], [2, 1, 1], [3, 2, 2], [22, 1, 3], [24, 1, 0x10000], [23, 2, 0x10001], [0, 3, 0x10002], [24, 4, 0x10003]
    ];
    const terrainObjectGridKeys = new Set([...forestCanopies, ...woodCanopies, ...hillTiles, ...mountainTiles].map(([x, y]) => `${x},${y}`));
    for (let y = 1; y <= 6; y++) for (let x = 0; x < width; x++) {
        if (!terrainObjectGridKeys.has(`${x},${y}`) && (x * 3 + y * 5) % 7 === 0) cells.push({ x, y, tile: 0 });
    }
    [[3, 7, 1], [20, 7, 1], [2, 11, 2], [22, 11, 2], [4, 17, 0], [20, 18, 0],
        [16, 9, 1], [8, 10, 2], [18, 16, 2], [6, 21, 0], [14, 20, 0]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile }));

    // Dense but broken woodland runs below the continuous Mangshan ridge without closing the river plain.
    forestCanopies
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 2 }));
    woodCanopies
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 6 }));

    // Mangshan foothills combine traversable hills with an impassable northern mountain ridge.
    hillTiles.forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 4 }));
    mountainTiles.forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 5 }));

    // Twelve crop, wheat, and paddy variations form the southern Luoyang farm plots.
    [[2, 18, 0], [3, 18, 1], [4, 18, 2], [5, 18, 3], [19, 18, 0x10000], [21, 18, 0x10001],
        [18, 19, 0x10002], [19, 19, 0x10003], [20, 19, 0x20000], [21, 19, 0x20001], [9, 21, 0x20002], [10, 21, 0x20003]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 7 }));

    // Reeds and pools show the eight swamp variants along both Luo River banks.
    [[3, 12, 0], [5, 12, 1], [6, 13, 2], [7, 13, 3], [17, 13, 0x10000], [18, 13, 0x10001], [19, 12, 0x10002], [21, 12, 0x10003]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 3 }));

    // North-bank river settlement and a pair of southern roadside farmhouses.
    [[9, 11, 0], [15, 11, 1], [7, 19, 2], [17, 20, 0]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 1 }));
    return cells;
}

function buildRiverEffect() {
    // Keep the animation on the deep Luo River only; ford and bank transition tiles stay static.
    return buildGround()
        .filter(({ tile }) => tile === 6)
        .map(({ x, y }) => ({ x, y, tile: 6 }));
}

function replaceLayer(scene, name, tileData) {
    const header = `[node name="${name}" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="${name === 'GroundLayer' ? 0 : 2}"]`;
    const node = `${header}\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = new RegExp(`\\[node name="${name}" parent="MapRoot"[^\\n]*\\][\\s\\S]*?(?=\\n\\[node |$)`);
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function addRiverEffectLayer(scene, tileData) {
    const node = `[node name="RiverEffectLayer" type="TileMapLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="4"]\nz_index = 1\nmaterial = SubResource("ShaderMaterial_luoyang_river")\ntile_map_data = PackedByteArray("${tileData}")\ntile_set = SubResource("TileSet_luoyang_river")\nrendering_quadrant_size = 8`;
    const expression = /\[node name="RiverEffectLayer" parent="MapRoot"[^\n]*\][\s\S]*?(?=\n\[node |$)/;
    return expression.test(scene)
        ? scene.replace(expression, node)
        : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function ensureRiverEffectResources(scene) {
    if (!scene.includes('id="3_floor"')) {
        scene = scene.replace(
            '[ext_resource type="Resource" path="res://data/scenarios/battle/field_luoyang.tres" id="2_scenario"]',
            '[ext_resource type="Resource" path="res://data/scenarios/battle/field_luoyang.tres" id="2_scenario"]\n[ext_resource type="Texture2D" path="res://assets/battle/floor/floor.png" id="3_floor"]\n[ext_resource type="Shader" path="res://assets/battle/floor/moat_water.gdshader" id="4_water_shader"]');
        scene = scene.replace(/\[gd_scene load_steps=\d+ format=4\]/, '[gd_scene load_steps=6 format=4]');
    }
    if (!scene.includes('id="TileSetAtlasSource_luoyang_river"')) {
        const resources = '[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_luoyang_river"]\ntexture = ExtResource("3_floor")\ntexture_region_size = Vector2i(128, 64)\nuse_texture_padding = false\n6:0/0 = 0\n\n[sub_resource type="TileSet" id="TileSet_luoyang_river"]\ntile_shape = 1\ntile_layout = 5\ntile_size = Vector2i(128, 64)\nsources/0 = SubResource("TileSetAtlasSource_luoyang_river")\n\n[sub_resource type="ShaderMaterial" id="ShaderMaterial_luoyang_river"]\nshader = ExtResource("4_water_shader")\n';
        scene = scene.replace('\n[node name="FieldBattleLuoyang"', `\n${resources}\n[node name="FieldBattleLuoyang"`);
    }
    return scene;
}

let scene = await fs.readFile(scenePath, 'utf8');
scene = ensureRiverEffectResources(scene);
scene = replaceLayer(scene, 'GroundLayer', encodeCells(buildGround()));
scene = replaceLayer(scene, 'ObjectLayer', encodeCells(buildObjects()));
scene = addRiverEffectLayer(scene, encodeCells(buildRiverEffect()));
await fs.writeFile(scenePath, scene, 'utf8');
console.log(`Remade ${scenePath} with the Luoyang river valley layout.`);
