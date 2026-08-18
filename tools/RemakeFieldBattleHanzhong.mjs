import fs from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const templatePath = path.join(root, 'scenes', 'battle', 'field', 'FieldBattleTemplate.tscn');
const scenePath = path.join(root, 'scenes', 'battle', 'field', 'FieldBattleHanzhong.tscn');
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

    // The Han River crosses the Hanzhong basin beneath the Qinling foothills.
    for (let x = 0; x < width; x++) {
        const riverY = x < 6 ? 10 : x < 15 ? 11 : 12;
        set(x, riverY, 6);
        set(x, riverY + 1, 6);
        set(x, riverY - 1, x % 4 === 0 ? 0x10002 : 0x10001); // Mud and Wet Grass on the floodplain edge.
        set(x, riverY + 2, x >= 10 && x <= 14 ? 0x10003 : 0x10004); // Pebble near the bridge, Shallow Water elsewhere.
    }

    // A western shallow ford offers a slower flank route. The stone bridge remains the fast, direct crossing.
    for (let x = 2; x <= 4; x++) {
        set(x, 10, 0x10004);
        set(x, 11, 0x10004);
    }

    // The maintained official road feeds directly into the two-cell stone bridge.
    for (let y = 5; y <= 17; y++) {
        if (y !== 11 && y !== 12) set(12, y, 0x10006);
    }
    for (let x = 4; x <= 21; x++) set(x, 18, 0x10005);
    for (let y = 18; y <= 23; y++) set(19, y, 0x10005);
    return cells;
}

function buildObjects() {
    const cells = [];
    const add = (x, y, tile, source = 0) => cells.push({ x, y, tile, source });

    // Qinling closes the north side of the valley; the Daba foothills press in from the south.
    for (let x = 0; x < width; x++) {
        add(x, 0, x % 4, 5);
        add(x, 1, (x + 1) % 4, 5);
        add(x, 24, (x + 2) % 4, 5);
    }
    for (const [from, to, y] of [[0, 5, 2], [18, 24, 2], [0, 3, 23], [21, 24, 23]]) {
        for (let x = from; x <= to; x++) add(x, y, (x + y) % 4, 5);
    }
    [[4, 3], [7, 3], [16, 3], [20, 3], [2, 22], [6, 22], [17, 22], [22, 22]]
        .forEach(([x, y], index) => add(x, y, index % 4, 4));

    // Dense mixed woodland along both mountain fronts creates flank and ambush routes.
    [[1, 3], [3, 4], [5, 5], [7, 4], [9, 3], [15, 3], [17, 4], [19, 5], [22, 4], [23, 6],
        [1, 20], [4, 21], [7, 22], [15, 21], [17, 20], [21, 21], [23, 20]]
        .forEach(([x, y], index) => add(x, y, index % 4, index % 2 === 0 ? 2 : 6));

    // Reeds and wet ground mark the river terraces without blocking all movement around the ford.
    [[2, 9], [4, 9], [6, 10], [8, 10], [16, 11], [18, 11], [20, 12], [23, 12],
        [3, 13], [6, 14], [17, 15], [22, 15]]
        .forEach(([x, y], index) => add(x, y, index % 4, 3));

    // Southern alluvial plain: fields and a small roadside settlement, using existing assets only.
    [[3, 17], [4, 17], [5, 17], [7, 19], [8, 19], [9, 19], [15, 19], [16, 19], [17, 19],
        [3, 20], [4, 20], [5, 20], [15, 20], [16, 20], [17, 20]]
        .forEach(([x, y], index) => add(x, y, index % 4, 7));
    [[9, 8, 0], [15, 9, 1], [6, 18, 2], [20, 17, 0]]
        .forEach(([x, y, tile]) => add(x, y, tile, 1));
    // bridge_01.png: the sole second-row tile forms both independent stone bridge cells.
    add(12, 11, 0x10000, 8);
    add(12, 12, 0x10000, 8);
    [[2, 7, 0], [7, 8, 1], [17, 7, 2], [22, 8, 0], [2, 16, 1], [23, 17, 2], [10, 21, 0]]
        .forEach(([x, y, tile]) => add(x, y, tile));
    return cells;
}

function buildRiverEffect() {
    // Keep the animated overlay limited to the deep Han River tiles.  The shallow-water
    // transition tiles remain static, and the overlay stays below bridge objects.
    return buildGround()
        .filter(({ tile }) => tile === 6)
        .map(({ x, y }) => ({ x, y, tile: 6 }));
}

function buildRiverObjects() {
    // source 9 is visual-only river decoration; bridge cells stay clear for gameplay readability.
    return [
        { x: 2, y: 10, tile: 0, source: 9 }, // ferry boat
        { x: 5, y: 11, tile: 1, source: 9 }, // skiff
        { x: 8, y: 11, tile: 0x10002, source: 9 }, // water disturbance
        { x: 16, y: 12, tile: 0x10000, source: 9 }, // reeds and lilies
        { x: 20, y: 13, tile: 0x10001, source: 9 } // river rock
    ];
}

function buildFarmObjects() {
    // source 10 is visual-only farm decoration; positions intentionally avoid crop, road, and building cells.
    return [
        { x: 2, y: 17, tile: 0, source: 10 }, // haystack
        { x: 6, y: 17, tile: 1, source: 10 }, // scarecrow
        { x: 14, y: 19, tile: 2, source: 10 }, // harvest cart
        { x: 9, y: 20, tile: 0x10000, source: 10 }, // grain sacks
        { x: 18, y: 20, tile: 0x10002, source: 10 } // produce basket
    ];
}

function buildForestEdgeObjects() {
    // source 11 is visual-only: scattered where the Qinling and Daba woodland opens toward the basin.
    return [
        { x: 2, y: 4, tile: 0, source: 11 }, // fallen log
        { x: 6, y: 5, tile: 1, source: 11 }, // stump
        { x: 16, y: 4, tile: 2, source: 11 }, // tall woodland tree
        { x: 21, y: 5, tile: 3, source: 11 }, // shrub
        { x: 2, y: 21, tile: 0x10001, source: 11 }, // mossy rock at the southern forest edge
        { x: 16, y: 20, tile: 0x10002, source: 11 } // bamboo clump
    ];
}

function replaceLayer(scene, name, tileData) {
    const header = `[node name="${name}" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="${name === 'GroundLayer' ? 0 : 2}"]`;
    const node = `${header}\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = new RegExp(`\\[node name="${name}" parent="MapRoot"[^\\n]*\\][\\s\\S]*?(?=\\n\\[node |$)`);
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function addRiverEffectLayer(scene, tileData) {
    const node = `[node name="RiverEffectLayer" type="TileMapLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="4"]\nz_index = 1\nmaterial = SubResource("ShaderMaterial_hanzhong_river")\ntile_map_data = PackedByteArray("${tileData}")\ntile_set = SubResource("TileSet_hanzhong_river")\nrendering_quadrant_size = 8`;
    return scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function replaceRiverObjectLayer(scene, tileData) {
    const node = `[node name="RiverObjectLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="5"]\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = /\[node name="RiverObjectLayer" parent="MapRoot"[^\n]*\][\s\S]*?(?=\n\[node |$)/;
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function replaceFarmObjectLayer(scene, tileData) {
    const node = `[node name="FarmObjectLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="6"]\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = /\[node name="FarmObjectLayer" parent="MapRoot"[^\n]*\][\s\S]*?(?=\n\[node |$)/;
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

function replaceForestEdgeObjectLayer(scene, tileData) {
    const node = `[node name="ForestEdgeObjectLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="7"]\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = /\[node name="ForestEdgeObjectLayer" parent="MapRoot"[^\n]*\][\s\S]*?(?=\n\[node |$)/;
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

let scene = await fs.readFile(templatePath, 'utf8');
scene = scene.replace('[gd_scene load_steps=2 format=4]', '[gd_scene load_steps=6 format=4]');
scene = scene.replace(
    '[ext_resource type="PackedScene" uid="uid://dxuj10r5yy13h" path="res://scenes/battle/BattleScene.tscn" id="1_base"]',
    '[ext_resource type="PackedScene" uid="uid://dxuj10r5yy13h" path="res://scenes/battle/BattleScene.tscn" id="1_base"]\n[ext_resource type="Resource" path="res://data/scenarios/battle/field_hanzhong.tres" id="2_scenario"]\n[ext_resource type="Texture2D" path="res://assets/battle/floor/floor.png" id="3_floor"]\n[ext_resource type="Shader" path="res://assets/battle/floor/moat_water.gdshader" id="4_water_shader"]');
scene = scene.replace('name="FieldBattleTemplate"', 'name="FieldBattleHanzhong"');
scene = scene.replace('ScenarioType = 1', 'ScenarioType = 1\nScenarioDefinition = ExtResource("2_scenario")');
scene = scene.replace(
    '[node name="FieldBattleHanzhong"',
    '[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_hanzhong_river"]\ntexture = ExtResource("3_floor")\ntexture_region_size = Vector2i(128, 64)\nuse_texture_padding = false\n6:0/0 = 0\n\n[sub_resource type="TileSet" id="TileSet_hanzhong_river"]\ntile_shape = 1\ntile_layout = 5\ntile_size = Vector2i(128, 64)\nsources/0 = SubResource("TileSetAtlasSource_hanzhong_river")\n\n[sub_resource type="ShaderMaterial" id="ShaderMaterial_hanzhong_river"]\nshader = ExtResource("4_water_shader")\n\n[node name="FieldBattleHanzhong"');
scene = replaceLayer(scene, 'GroundLayer', encodeCells(buildGround()));
scene = replaceLayer(scene, 'ObjectLayer', encodeCells(buildObjects()));
scene = addRiverEffectLayer(scene, encodeCells(buildRiverEffect()));
scene = replaceRiverObjectLayer(scene, encodeCells(buildRiverObjects()));
scene = replaceFarmObjectLayer(scene, encodeCells(buildFarmObjects()));
scene = replaceForestEdgeObjectLayer(scene, encodeCells(buildForestEdgeObjects()));
await fs.writeFile(scenePath, scene, 'utf8');
console.log(`Remade ${scenePath} with the Hanzhong basin layout.`);
