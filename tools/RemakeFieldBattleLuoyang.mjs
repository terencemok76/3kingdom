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
        if (x === 6 || x === 7 || x === 17 || x === 18) set(x, riverY - 1, 7);
        set(x, Math.min(height - 1, riverY + 2), 0x10000);
    }

    for (const x of [11, 12, 13]) for (let y = 12; y <= 16; y++) set(x, y, 1);
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
    for (let y = 1; y <= 6; y++) for (let x = 0; x < width; x++) {
        if ((x * 3 + y * 5) % 7 === 0) cells.push({ x, y, tile: 0 });
    }
    [[3, 7, 1], [20, 7, 1], [2, 11, 2], [22, 11, 2], [4, 17, 0], [20, 18, 0],
        [16, 9, 1], [8, 10, 2], [18, 16, 2], [6, 21, 0], [14, 20, 0]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile }));

    // North-bank river settlement and a pair of southern roadside farmhouses.
    [[9, 11, 0], [15, 11, 1], [7, 19, 2], [17, 20, 0]]
        .forEach(([x, y, tile]) => cells.push({ x, y, tile, source: 1 }));
    return cells;
}

function replaceLayer(scene, name, tileData) {
    const header = `[node name="${name}" parent="MapRoot" parent_id_path=PackedInt32Array(105299395) index="${name === 'GroundLayer' ? 0 : 2}"]`;
    const node = `${header}\ntile_map_data = PackedByteArray("${tileData}")`;
    const expression = new RegExp(`\\[node name="${name}" parent="MapRoot"[^\\n]*\\][\\s\\S]*?(?=\\n\\[node |$)`);
    return expression.test(scene) ? scene.replace(expression, node) : scene.replace(/\n\[node name="MoatLayer"/, `\n${node}\n\n[node name="MoatLayer"`);
}

let scene = await fs.readFile(scenePath, 'utf8');
scene = replaceLayer(scene, 'GroundLayer', encodeCells(buildGround()));
scene = replaceLayer(scene, 'ObjectLayer', encodeCells(buildObjects()));
await fs.writeFile(scenePath, scene, 'utf8');
console.log(`Remade ${scenePath} with the Luoyang river valley layout.`);
