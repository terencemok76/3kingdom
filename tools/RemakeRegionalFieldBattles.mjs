import fs from 'node:fs/promises';
import path from 'node:path';

// Builds the six first regional Field Battle scenes from the same editor-authored
// TileMap structure as Hanzhong.  Ground is gameplay terrain; object sources 2-8
// are read by BattleMapData as forest, swamp, hill, mountain, farm and bridge.
const root = path.resolve(import.meta.dirname, '..');
const templatePath = path.join(root, 'scenes', 'battle', 'field', 'FieldBattleHanzhong.tscn');
const width = 25;
const height = 25;
const emitPatch = process.argv.includes('--emit-patch');
const emitResourcesOnly = process.argv.includes('--resources-only');
const targetKey = process.argv.find(argument => argument.startsWith('--target='))?.split('=')[1];
const patches = [];
const spawnOverrides = {
    ye: { AttackerA: [10, 21], AttackerB: [14, 21], AttackerC: [21, 21], AttackerWorker: [15, 22], Catapult: [14, 20], Spearman: [8, 20], SupplyCart: [18, 22], DefenderA: [10, 4], DefenderB: [14, 5], DefenderC: [12, 5], Worker: [16, 4] },
    jinyang: { AttackerA: [10, 21], AttackerB: [12, 21], AttackerC: [14, 21], AttackerWorker: [13, 22], Catapult: [13, 20], Spearman: [9, 20], SupplyCart: [15, 22], DefenderA: [11, 8], DefenderB: [15, 6], DefenderC: [12, 9], Worker: [15, 5] },
    xiapi: { AttackerA: [14, 20], AttackerB: [16, 20], AttackerC: [14, 22], AttackerWorker: [18, 21], Catapult: [12, 19], Spearman: [11, 21], SupplyCart: [20, 20], DefenderA: [14, 5], DefenderB: [17, 5], DefenderC: [14, 7], Worker: [20, 6] },
    jianye: { AttackerA: [12, 21], AttackerB: [14, 21], AttackerC: [18, 21], AttackerWorker: [15, 21], Catapult: [13, 20], Spearman: [10, 20], SupplyCart: [15, 22], DefenderA: [10, 5], DefenderB: [14, 5], DefenderC: [12, 7], Worker: [16, 5] },
    xiangyang: { AttackerA: [10, 21], AttackerB: [12, 21], AttackerC: [14, 21], AttackerWorker: [13, 22], Catapult: [14, 16], Spearman: [9, 20], SupplyCart: [15, 22], DefenderA: [10, 8], DefenderB: [14, 8], DefenderC: [12, 8], Worker: [13, 6] },
    jiangling: { AttackerA: [10, 21], AttackerB: [12, 20], AttackerC: [8, 21], AttackerWorker: [13, 17], Catapult: [14, 16], Spearman: [10, 19], SupplyCart: [14, 17], DefenderA: [10, 4], DefenderB: [14, 5], DefenderC: [12, 5], Worker: [16, 4] }
};
const groundTile = { grass: 0, wet: 0x10001, mud: 0x10002, pebble: 0x10003, shallow: 0x10004, wornRoad: 0x10005, officialRoad: 0x10006, dryRiver: 0x10007, river: 6 };

function encodeCells(cells) {
    const buffer = Buffer.alloc(cells.length * 12 + 2);
    // TileMapLayer serializes a two-byte format header followed by x, y, source,
    // atlas x, atlas y and alternative tile (all uint16). The alternative field
    // carries TileSetAtlasSource.TransformFlipH (4096) for horizontally flipped art.
    buffer.writeUInt16LE(0, 0);
    cells.sort((a, b) => a.y - b.y || a.x - b.x).forEach((cell, index) => {
        const offset = 2 + index * 12;
        buffer.writeUInt16LE(cell.x, offset);
        buffer.writeUInt16LE(cell.y, offset + 2);
        buffer.writeUInt16LE(cell.source ?? 0, offset + 4);
        buffer.writeUInt16LE(cell.tile & 0xffff, offset + 6);
        buffer.writeUInt16LE(cell.tile >>> 16, offset + 8);
        buffer.writeUInt16LE(cell.alternative ?? 0, offset + 10);
    });
    return buffer.toString('base64');
}

function replaceLayer(scene, name, cells, index) {
    const node = `[node name="${name}" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="${index}"]\ntile_map_data = PackedByteArray("${encodeCells(cells)}")`;
    const expression = new RegExp(`\\[node name="${name}" parent="MapRoot"[^\\n]*\\][\\s\\S]*?(?=\\n\\[node |$)`);
    return scene.replace(expression, node);
}

function replaceRiverEffect(scene, cells) {
    const node = `[node name="RiverEffectLayer" type="TileMapLayer" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="4"]\nz_index = 1\nmaterial = SubResource("ShaderMaterial_hanzhong_river")\ntile_map_data = PackedByteArray("${encodeCells(cells)}")\ntile_set = SubResource("TileSet_hanzhong_river")\nrendering_quadrant_size = 8`;
    return scene.replace(/\[node name="RiverEffectLayer"[^\n]*\][\s\S]*?(?=\n\[node |$)/, node);
}

async function buildFilePatch(filePath, content) {
    try {
        const previous = await fs.readFile(filePath, 'utf8');
        return `*** Update File: ${filePath}\n@@\n${previous.split('\n').filter((_, index, lines) => index < lines.length - 1).map(line => `-${line}`).join('\n')}\n${content.split('\n').filter((_, index, lines) => index < lines.length - 1).map(line => `+${line}`).join('\n')}`;
    } catch (error) {
        if (error.code !== 'ENOENT') throw error;
        return `*** Add File: ${filePath}\n${content.split('\n').filter((_, index, lines) => index < lines.length - 1).map(line => `+${line}`).join('\n')}`;
    }
}

function cellsFor(config) {
    const ground = Array.from({ length: width * height }, (_, index) => ({ x: index % width, y: Math.floor(index / width), tile: groundTile.grass }));
    const objects = [];
    const riverObjects = [];
    const farmObjects = [];
    const forestEdgeObjects = [];
    const setGround = (x, y, tile) => {
        if (x >= 0 && x < width && y >= 0 && y < height) ground[y * width + x].tile = tile;
    };
    const line = (x1, y1, x2, y2, tile) => {
        const count = Math.max(Math.abs(x2 - x1), Math.abs(y2 - y1));
        for (let i = 0; i <= count; i++) setGround(Math.round(x1 + (x2 - x1) * i / count), Math.round(y1 + (y2 - y1) * i / count), tile);
    };
    const area = (x1, y1, x2, y2, tile) => {
        for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) setGround(x, y, tile);
    };
    const add = (target, x, y, source, tile = 0, alternative = 0) => target.push({ x, y, source, tile, alternative });
    const objectLine = (x1, y1, x2, y2, source) => {
        const count = Math.max(Math.abs(x2 - x1), Math.abs(y2 - y1));
        for (let i = 0; i <= count; i++) add(objects, Math.round(x1 + (x2 - x1) * i / count), Math.round(y1 + (y2 - y1) * i / count), source, i % 4);
    };

    config.paint({ setGround, line, area, add, objectLine, objects, riverObjects, farmObjects, forestEdgeObjects });
    const bridgeGrids = new Set(objects
        .filter(({ source }) => source === 8)
        .map(({ x, y }) => `${x},${y}`));
    const riverEffect = ground
        .filter(({ x, y, tile }) => tile === groundTile.river && !bridgeGrids.has(`${x},${y}`))
        .map(({ x, y }) => ({ x, y, tile: groundTile.river }));
    return { ground, objects, riverEffect, riverObjects, farmObjects, forestEdgeObjects };
}

const scenarios = [
    {
        key: 'ye', name: 'Ye', displayName: 'Field Battle (Ye)', weather: 0, time: 2,
        paint: ({ line, area, add, objectLine, objects, farmObjects, forestEdgeObjects }) => {
            // Ye is an open Jizhou field battle.  The shallow dry creek is a road, not a river barrier:
            // crops disrupt frontal cavalry Charges while both wide grass wings remain manoeuvre space.
            line(0, 8, 24, 9, groundTile.dryRiver);
            line(12, 2, 12, 22, groundTile.officialRoad);
            line(2, 17, 22, 17, groundTile.wornRoad);
            [[5, 10, 9, 14], [14, 10, 18, 14], [3, 19, 7, 22], [10, 19, 13, 22], [16, 19, 20, 22]].forEach(([x1, y1, x2, y2]) => {
                for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4);
            });
            [[2, 3], [22, 3]].forEach(([x, y], i) => add(objects, x, y, 4, i));
            [[3, 18], [9, 19], [14, 19], [21, 20]].forEach(([x, y], i) => add(farmObjects, x, y, 10, i));
            [[1, 5], [23, 5]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i));
        }
    },
    {
        key: 'jinyang', name: 'Jinyang', displayName: 'Field Battle (Jinyang)', weather: 1, time: 1,
        paint: ({ setGround, line, area, add, objectLine, objects, riverObjects, farmObjects, forestEdgeObjects }) => {
            // Fen River valley: the single two-cell river and sole bridge sit inside a real gorge.
            // Three-cell mountain masses close both flanks on each bank, leaving only the central valley floor open.
            area(0, 11, 24, 12, groundTile.river);
            line(11, 3, 11, 22, groundTile.officialRoad); line(3, 19, 21, 19, groundTile.wornRoad);
            [[11, 11], [11, 12]].forEach(([x, y]) => { setGround(x, y, groundTile.river); add(objects, x, y, 8, 0x10000); });
            [[0, 1, 4, 6], [20, 1, 24, 6], [0, 18, 4, 23], [20, 18, 24, 23]].forEach(([x1, y1, x2, y2], rangeIndex) => {
                for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 5, (x + y + rangeIndex) % 8);
            });
            [[5, 5, 7, 9], [17, 5, 19, 9], [5, 15, 7, 18], [17, 15, 19, 18]].forEach(([x1, y1, x2, y2], rangeIndex) => {
                for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 4, (x + y + rangeIndex) % 8);
            });
            [[8, 7], [16, 7], [8, 16], [16, 16]].forEach(([x, y], i) => add(objects, x, y, 2, i));
            // The bridge-exit outpost controls the main road; the eastern reserve outpost anchors the valley flank by its mountain shoulder.
            [[11, 8], [15, 6]].forEach(([x, y], i) => add(objects, x, y, 12, i));
            // Two small buildings form the bridge-exit's left and right infantry cover without blocking the road or outpost.
            [[10, 8], [12, 8]].forEach(([x, y], i) => add(objects, x, y, 1, i));
            [[6, 20, 7, 22], [17, 20, 18, 22]].forEach(([x1, y1, x2, y2]) => { for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4); });
            // Three visual-only groups make the gorge legible at overview scale: busy riverbanks,
            // farm hamlets on the south approach and broad wooded shoulders on both sides.
            [[2, 11], [4, 12], [5, 11], [6, 12], [7, 11], [16, 12], [18, 11], [19, 11], [20, 12], [22, 12]].forEach(([x, y], i) => add(riverObjects, x, y, 9, i % 8));
            [[5, 20], [5, 21], [6, 21], [6, 22], [7, 21], [7, 22], [17, 20], [17, 21], [18, 20], [18, 22], [19, 21], [19, 22]].forEach(([x, y], i) => add(farmObjects, x, y, 10, i % 8));
            [[6, 6], [7, 6], [8, 6], [6, 7], [8, 7], [6, 8], [7, 8], [8, 8], [17, 6], [18, 6], [19, 6], [16, 7], [18, 7], [17, 8], [18, 8], [19, 8], [5, 16], [6, 16], [7, 16], [8, 16], [6, 17], [7, 17], [8, 17], [16, 16], [17, 16], [18, 16], [19, 16], [17, 17], [18, 17], [19, 17]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i % 8));
        }
    },
    {
        key: 'xiapi', name: 'Xiapi', displayName: 'Field Battle (Xiapi)', weather: 2, time: 1,
        paint: ({ setGround, line, area, add, objects, riverObjects, farmObjects, forestEdgeObjects }) => {
            // Xiapi is a Si River water network: three land pockets, two small bridges, and dikes.
            line(0, 9, 24, 10, groundTile.river); line(0, 10, 24, 11, groundTile.river);
            line(7, 0, 7, 24, groundTile.river); line(8, 0, 8, 24, groundTile.river);
            line(17, 2, 17, 22, groundTile.wornRoad); line(9, 18, 23, 18, groundTile.officialRoad);
            // Xiapi keeps two bridge tiles at each crossing. The left/southern pair
            // uses Flip H, while the right/eastern pair keeps bridge_01's original art direction.
            [[7, 17], [8, 17]].forEach(([x, y]) => { setGround(x, y, groundTile.river); add(objects, x, y, 8, 0x10000, 0x1000); });
            [[17, 10], [17, 11]].forEach(([x, y]) => { setGround(x, y, groundTile.river); add(objects, x, y, 8, 0x10000); });
            area(1, 12, 5, 21, groundTile.mud); area(10, 3, 15, 7, groundTile.mud); area(19, 13, 23, 22, groundTile.wet);
            [[2, 10], [5, 14], [10, 10], [15, 10], [20, 11], [8, 6]].forEach(([x, y], i) => add(riverObjects, x, y, 9, i));
            [[19, 19, 22, 22]].forEach(([x1, y1, x2, y2]) => { for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4); });
            [[2, 16], [4, 20], [20, 5], [22, 16]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i));
        }
    },
    {
        key: 'jianye', name: 'Jianye', displayName: 'Field Battle (Jianye)', weather: 1, time: 2,
        paint: ({ line, area, add, objectLine, objects, riverObjects, farmObjects, forestEdgeObjects }) => {
            // Jianye has no crossing: the Yangtze blocks one edge while Jiangdong hills form the other.
            line(0, 0, 0, 24, groundTile.river); line(1, 0, 1, 24, groundTile.river);
            line(8, 3, 8, 22, groundTile.officialRoad); line(8, 17, 19, 17, groundTile.wornRoad);
            objectLine(18, 0, 24, 6, 5); objectLine(20, 8, 24, 18, 5); objectLine(16, 22, 24, 24, 5);
            [[15, 3], [18, 5], [20, 8], [18, 11], [21, 14], [17, 18], [22, 20]].forEach(([x, y], i) => add(objects, x, y, 2, i));
            [[13, 5], [16, 7], [15, 12], [18, 16], [13, 20]].forEach(([x, y], i) => add(objects, x, y, 4, i));
            [[4, 19, 7, 22], [10, 19, 13, 22]].forEach(([x1, y1, x2, y2]) => { for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4); });
            [[0, 5], [0, 12], [1, 19]].forEach(([x, y], i) => add(riverObjects, x, y, 9, i));
            [[16, 4], [20, 9], [21, 15]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i));
        }
    },
    {
        key: 'xiangyang', name: 'Xiangyang', displayName: 'Field Battle (Xiangyang)', weather: 0, time: 1,
        paint: ({ setGround, line, area, add, objectLine, objects, riverObjects, farmObjects, forestEdgeObjects }) => {
            // Xiangyang is a bridgehead: the Han River has one decisive bridge, but unlike
            // Jinyang its northern bank is a broad, passable hill platform for missile troops.
            area(0, 11, 24, 12, groundTile.river);
            line(12, 3, 12, 21, groundTile.officialRoad); line(4, 18, 21, 18, groundTile.wornRoad);
            [[12, 11], [12, 12]].forEach(([x, y]) => { setGround(x, y, groundTile.river); add(objects, x, y, 8, 0x10000); });
            [[0, 0, 8, 2], [16, 0, 24, 2]].forEach(([x1, y1, x2, y2], rangeIndex) => {
                for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 5, (x + y + rangeIndex) % 8);
            });
            [[1, 3, 9, 8], [15, 3, 23, 8], [6, 9, 10, 10], [14, 9, 18, 10]].forEach(([x1, y1, x2, y2], rangeIndex) => {
                for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 4, (x + y + rangeIndex) % 8);
            });
            [[10, 4], [14, 4], [10, 6], [14, 6], [10, 8], [14, 8]].forEach(([x, y], i) => add(objects, x, y, 2, i));
            // Four objectives make capture victory a deliberate flanking operation: the east bridgehead,
            // a northern reserve, and one strongpoint in each hill defense zone.
            [[4, 4], [19, 4], [10, 5], [14, 8]].forEach(([x, y], i) => {
                for (let index = objects.length - 1; index >= 0; index--) {
                    if (objects[index].x === x && objects[index].y === y) objects.splice(index, 1);
                }
                add(objects, x, y, 12, i % 2);
            });
            [[2, 18, 5, 22], [19, 18, 22, 22]].forEach(([x1, y1, x2, y2]) => { for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4); });
            [[3, 11], [8, 11], [17, 12], [22, 12]].forEach(([x, y], i) => add(riverObjects, x, y, 9, i));
            [[3, 20], [5, 21], [20, 20], [22, 21]].forEach(([x, y], i) => add(farmObjects, x, y, 10, i));
            [[10, 4], [14, 4], [10, 6], [14, 6]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i));
        }
    },
    {
        key: 'jiangling', name: 'Jiangling', displayName: 'Field Battle (Jiangling)', weather: 2, time: 1,
        paint: ({ line, area, add, objects, riverObjects, farmObjects, forestEdgeObjects }) => {
            // Jiangling is not a bridge map.  Its Jianghan plain is a marsh maze with two raised dikes
            // and several slow shallow channels; choosing a dike protects movement but exposes troops.
            line(2, 4, 22, 6, groundTile.shallow); line(1, 14, 20, 16, groundTile.shallow);
            line(5, 3, 5, 22, groundTile.wornRoad); line(15, 2, 15, 22, groundTile.wornRoad);
            line(5, 18, 20, 18, groundTile.officialRoad);
            area(0, 7, 4, 12, groundTile.mud); area(7, 7, 12, 13, groundTile.mud); area(17, 7, 23, 13, groundTile.mud); area(8, 20, 13, 23, groundTile.wet);
            [[2, 9], [4, 11], [8, 8], [10, 12], [18, 9], [21, 11], [9, 21]].forEach(([x, y], i) => add(objects, x, y, 3, i));
            [[3, 5], [10, 6], [19, 6], [4, 15], [12, 16], [19, 16]].forEach(([x, y], i) => add(riverObjects, x, y, 9, i));
            [[11, 19, 14, 22], [17, 19, 20, 22]].forEach(([x1, y1, x2, y2]) => { for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) add(objects, x, y, 7, (x + y) % 4); });
            [[12, 19], [19, 20]].forEach(([x, y], i) => add(farmObjects, x, y, 10, i));
            [[1, 8], [22, 8], [2, 19]].forEach(([x, y], i) => add(forestEdgeObjects, x, y, 11, i));
        }
    }
];

for (const scenario of scenarios.filter(scenario => !targetKey || scenario.key === targetKey)) {
    const map = cellsFor(scenario);
    const scenePath = path.join(root, 'scenes', 'battle', 'field', `FieldBattle${scenario.name}.tscn`);
    const resourcePath = path.join(root, 'data', 'scenarios', 'battle', `field_${scenario.key}.tres`);
    let scene = await fs.readFile(templatePath, 'utf8');
    // A generated scene must not inherit the template scene UID. Godot will assign
    // one on its next writable editor import, avoiding duplicate-scene identity.
    scene = scene.replace(/^\[gd_scene format=4 uid="[^"]+"\]/, '[gd_scene format=4]');
    scene = scene.replaceAll('FieldBattleHanzhong', `FieldBattle${scenario.name}`);
    scene = scene.replaceAll('field_hanzhong.tres', `field_${scenario.key}.tres`);
    scene = replaceLayer(scene, 'GroundLayer', map.ground, 0);
    scene = replaceLayer(scene, 'ObjectLayer', map.objects, 2);
    scene = replaceRiverEffect(scene, map.riverEffect);
    scene = replaceLayer(scene, 'RiverObjectLayer', map.riverObjects, 5);
    scene = replaceLayer(scene, 'FarmObjectLayer', map.farmObjects, 6);
    scene = replaceLayer(scene, 'ForestEdgeObjectLayer', map.forestEdgeObjects, 7);
    if (emitPatch && !emitResourcesOnly) {
        patches.push(await buildFilePatch(scenePath, scene));
    } else if (!emitPatch) {
        await fs.writeFile(scenePath, scene, 'utf8');
    }

    const spawnEntries = Object.entries(spawnOverrides[scenario.key])
        .map(([unit, [x, y]]) => `"${unit}": Vector2i(${x}, ${y})`)
        .join(',\n');
    const resource = `[gd_resource type="Resource" script_class="BattleScenarioDefinition" format=3]\n\n[ext_resource type="Script" uid="uid://ctlxf41bf1vrk" path="res://scripts/battle/BattleScenarioDefinition.cs" id="1_field_${scenario.key}"]\n\n[resource]\nscript = ExtResource("1_field_${scenario.key}")\nDisplayName = "${scenario.displayName}"\nScenarioType = 1\nWeather = ${scenario.weather}\nWindDirection = 2\nWindPower = 1\nTimeOfDay = ${scenario.time}\nUnitSpawnGrids = Dictionary[String, Vector2i]({\n${spawnEntries}\n})\n`;
    if (emitPatch) {
        patches.push(await buildFilePatch(resourcePath, resource));
    } else {
        await fs.writeFile(resourcePath, resource, 'utf8');
        console.log(`Remade ${path.relative(root, scenePath)}.`);
    }
}

if (emitPatch) {
    console.log(`*** Begin Patch\n${patches.join('\n')}\n*** End Patch`);
}
