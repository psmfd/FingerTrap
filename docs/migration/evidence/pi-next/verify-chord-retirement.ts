import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const checkout = resolve(process.argv[2]);
// A bounded stack exposes the original internal argument expansion while both
// native push and the replacement still accept the same public 100,000 inputs.
// Default-stack thresholds vary; keep this explicit instead of enlarging payloads
// beyond the replacement's public JavaScript call envelope.
if (!process.execArgv.includes('--stack_size=850')) {
    const result = spawnSync(process.execPath, ['--stack_size=850', '--experimental-strip-types', fileURLToPath(import.meta.url), checkout], {stdio:'inherit'});
    if (result.error) throw result.error;
    process.exit(result.status ?? 1);
}
const scratch = mkdtempSync(join(tmpdir(), 'ft-chord-retirement-'));
const original = execFileSync('git', ['-C', checkout, 'show', 'f07218c4d4bbc12bef056a7058c3dd49dfe41abe:packages/chord/src/delta/index.ts']);
const oldModule = join(scratch, 'original.ts');
writeFileSync(oldModule, original);
try {
const { track: oldTrack } = await import(pathToFileURL(oldModule).href);
const { track, apply } = await import(pathToFileURL(join(checkout, 'packages/chord/src/delta/index.ts')).href);

const payload = Array.from({length:100_000},(_,index)=>index);
const old = oldTrack({xs: [] as number[]});
old.flush();
assert.throws(()=>Reflect.apply(old.state.xs.push, old.state.xs, payload), RangeError);
const tracker=track({xs: [] as number[]});
const change=tracker.beginChange();
const expected: number[]=[];
assert.equal(Reflect.apply(expected.push,expected,payload), payload.length);
assert.equal(Reflect.apply(change.state.xs.push,change.state.xs,payload), payload.length);
const prepared=change.prepare();
assert.deepEqual(prepared.value.xs, expected);
assert.deepEqual(apply({xs: []},prepared.ops),{xs:expected});
assert.deepEqual(tracker.value.xs,[]);
tracker.adopt(prepared);
assert.deepEqual(tracker.value.xs,expected);
for (const index of [0,2,4]) for (const remove of [0,1,3]) for (const items of [[],[8],[8,9,10]]) {
    const initial={xs:[0,1,2,3]};
    const native=initial.xs.slice();
    const t=track(initial);
    const c=t.beginChange();
    assert.deepEqual(c.state.xs.splice(index,remove,...items),native.splice(index,remove,...items));
    const p=c.prepare();
    assert.deepEqual(p.value,{xs:native});
    assert.deepEqual(apply(structuredClone(initial),p.ops),{xs:native});
}
const initial={xs:Array.from({length:20_000},(_,index)=>index)};
const middle=track(initial).beginChange();
const native=initial.xs.slice();
const args=[10_000,2,...Array.from({length:20_000},(_,index)=>-index-1)];
assert.deepEqual(Reflect.apply(middle.state.xs.splice,middle.state.xs,args),Reflect.apply(native.splice,native,args));
const p=middle.prepare();
assert.deepEqual(p.value,{xs:native});
assert.deepEqual(apply(structuredClone(initial),p.ops),{xs:native});
console.log(JSON.stringify({node:process.version,stackKiB:850,baseline:'f07218c4d4bbc12bef056a7058c3dd49dfe41abe',selectedPayload:payload.length,baselineOriginal:'RangeError reproduced while native push passes',latest:'exact content, operations, immutable preparation and adoption pass',nativeSpliceCases:27,middle20000:'exact content and replay pass'}));

} finally { rmSync(scratch, {recursive:true,force:true}); }
