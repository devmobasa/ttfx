"""Benchmark complete CLI animations, validating compiled output before timing.

No asm/fx; one pinned CPU, two warmups, eleven measured repetitions by default.
Python keeps its native RNG and ordering; its output is contextual, not an oracle.
"""
import argparse
import hashlib
import json
import math
import os
import platform
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--rust', type=Path, required=True)
parser.add_argument('--csharp-artifacts', type=Path, help='Directory containing linux-x64-v2/v3/v4 publishes')
parser.add_argument('--python-reference', type=Path, required=True)
parser.add_argument('--cpu', type=int, required=True)
parser.add_argument('--sizes', nargs='+', default=['200x50', '400x100'])
parser.add_argument('--repeats', type=int, default=11)
parser.add_argument('--warmups', type=int, default=2)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--effects', nargs='+')
parser.add_argument('--verify-only', action='store_true')
args = parser.parse_args()
ROOT = Path(__file__).resolve().parents[1]
args.output = args.output.resolve()
args.output.mkdir(parents=True, exist_ok=True)
os.sched_setaffinity(0, {args.cpu})
env = {key:value for key,value in os.environ.items() if not key.startswith(('TTFX_', 'DOTNET_', 'COMPlus_'))}
env.update(TTFX_ASM='0', TTFX_THREADS='1', DOTNET_PROCESSOR_COUNT='1', DOTNET_gcServer='0',
           PYTHONPATH=str(args.python_reference.resolve()), PYTHONHASHSEED='0')
artifact_root = args.csharp_artifacts.resolve() if args.csharp_artifacts else ROOT/'artifacts'
original_paths = {'rust033':args.rust.resolve(), **{tier:artifact_root/f'linux-x64-{tier}/ttfx' for tier in ['v2','v3','v4']}}
snapshot = args.output/'.binary-snapshots'
snapshot.mkdir(parents=True, exist_ok=True)
paths = {}
for key, path in original_paths.items():
    target = snapshot/key
    if not target.exists() or hashlib.sha256(target.read_bytes()).digest() != hashlib.sha256(path.read_bytes()).digest():
        shutil.copy2(path, target)
    paths[key] = target

report_path = args.output/'samples.json'
if report_path.exists():
    report = json.loads(report_path.read_text())
    hashes = {key:hashlib.sha256(path.read_bytes()).hexdigest() for key,path in paths.items()}
    if any(key not in hashes or hashes[key] != value for key,value in report['binary_sha256'].items()):
        raise RuntimeError('Binary hashes changed; use a new output directory to avoid mixing benchmark artifacts.')
    report['binary_sha256'] = hashes
    report.update(repeats=args.repeats,warmups=args.warmups)
    report['sample_policy']='First requested number of samples; extra completed samples retained but unused.'
else:
    report = {'csharp_revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              'rust_revision':'54d21f046f22512b113056a1964077d7b7bf04cc',
              'python_revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=args.python_reference,text=True).strip(),
              'csharp_dirty':bool(subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True).strip()),
              'source_files_sha256':{str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted((ROOT/'src').rglob('*')) if path.is_file() and 'bin' not in path.parts and 'obj' not in path.parts},
              'python_version':sys.version,'platform':platform.platform(),'libc':platform.libc_ver(),
              'cpu':args.cpu,'smt_siblings':Path(f'/sys/devices/system/cpu/cpu{args.cpu}/topology/thread_siblings_list').read_text().strip(),
              'machine':'16-core processor (Linux)','rustc':subprocess.check_output(['rustc','-vV'],text=True),
              'dotnet_sdk':subprocess.check_output(['dotnet','--version'],cwd=ROOT,text=True).strip(),
              'rust_build':'cargo build --release --locked; Rust 0.3.3; baseline CPU; no assembly engine',
              'csharp_build':'NativeAOT Release; OptimizationPreference=Speed; x86-64-v2/v3/v4',
              'seed':1,'repeats':args.repeats,'warmups':args.warmups,
              'environment':{key:env[key] for key in ['TTFX_ASM','TTFX_THREADS','DOTNET_PROCESSOR_COUNT','DOTNET_gcServer','PYTHONHASHSEED']},
              'python_method':'Upstream CLI, native RNG and ordering; clock-only adapter for matrix/thunderstorm. Output may differ from Rust/C#.',
              'binary_sha256':{key:hashlib.sha256(path.read_bytes()).hexdigest() for key,path in paths.items()},
              'sizes':{},'started_utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())}

def await_quiet():
    while True:
        active = []
        for proc in Path('/proc').iterdir():
            if not proc.name.isdigit():continue
            try:
                name = (proc/'comm').read_text().strip()
                state = (proc/'stat').read_text().split(') ',1)[1].split()[0]
                if name in {'rustc','csc','clippy-driver','ilc','ninja','clang','gcc'} and state != 'Z':active.append(name)
            except (FileNotFoundError,PermissionError,ProcessLookupError):pass
        if not active:return
        print('Waiting for quiet machine: '+', '.join(sorted(set(active))),flush=True)
        time.sleep(5)

def save():
    report_path.write_text(json.dumps(report,indent=2)+'\n')

def tables():
    if 'best_tier' not in report:return
    best = report['best_tier']
    dirty_label = ' + uncommitted changes' if report.get('csharp_dirty') else ''
    lines = ['# C# / Python / Rust 0.3.3: all 37 effects', '',
             f"C# `{report['csharp_revision'][:7]}`{dirty_label}, Rust `54d21f0`, Python `{report['python_revision'][:7]}`. 16-core processor (Linux), pinned to one CPU core with one worker thread per implementation. Full animations, seed 1, pacing off. Median of {args.repeats} runs after {args.warmups} warmups.", '',
             f'Best C# tier: **{best}**, chosen once using the geometric mean of all 37 effects at 200×50, then used for every effect. Portable v2 is shown alongside it.', '',
             'Compiled outputs must be byte-identical before timing. Python uses its native RNG and is a contextual comparison. Matrix/thunderstorm use a virtual clock on every implementation; the Python adapter changes only the clock and advances it after each printed frame. No asm or fx.', '']
    for size, batch in report['sizes'].items():
        lines += [f'## {size}', '', '| Effect | Python ms | ttfx 0.3.3 (Rust) ms | C# '+best+' ms | vs Rust | vs Python | C# v2 ms | v2 vs Rust |', '|---|---:|---:|---:|---:|---:|---:|---:|']
        ratios=[];portable=[]
        for effect,row in batch['results'].items():
            if not all(key in row.get('median_ms',{}) for key in ['python','rust033',best,'v2']):continue
            m=row['median_ms'];r=m['rust033']/m[best];v=m['rust033']/m['v2'];ratios.append(r);portable.append(v)
            lines.append(f"| {effect} | {m['python']:,.2f} | {m['rust033']:,.2f} | {m[best]:,.2f} | {r:.2f}× | {m['python']/m[best]:.1f}× | {m['v2']:,.2f} | {v:.2f}× |")
        if ratios:lines.append(f"| **Geometric mean ({len(ratios)} effects)** | | | | **{math.exp(statistics.mean(map(math.log,ratios))):.2f}×** | | | **{math.exp(statistics.mean(map(math.log,portable))):.2f}×** |")
        lines.append('')
    lines += [f'Requested run: {", ".join(args.sizes)}, {args.repeats} measured samples. Medians use the first {args.repeats} samples; any extra completed samples remain in the raw data. The runner waited for known compiler processes between repetitions, but this was an active desktop, not a dedicated benchmark machine.', '', 'A speedup below 1× means C# is slower. `samples.json` contains every individual sample, output hashes, environment, machine details and load readings.','']
    (args.output/'results.md').write_text('\n'.join(lines))

help_text = subprocess.check_output([str(paths['rust033']),'--help'],env=env,text=True)
effects = args.effects or [line.split()[0] for line in help_text.split('Commands:',1)[1].split('Options:',1)[0].splitlines() if line.strip() and line.split()[0]!='help']
for size in args.sizes:
    columns,rows = map(int,size.split('x'))
    env.update(COLUMNS=str(columns),LINES=str(rows))
    width = max(1,columns-10)
    text_lines = [f'benchmark line {i:03d} — the quick brown fox jumps over the lazy dog' for i in range(max(1,rows-4))]
    data = '\n'.join((line*(width//len(line)+1))[:width] for line in text_lines).encode()
    batch = report['sizes'].setdefault(size,{'input_sha256':hashlib.sha256(data).hexdigest(),'input_bytes':len(data),'input_rows':len(text_lines),'input_columns':width,'results':{}})
    keys = ['rust033','v2','v3','v4','python'] if size=='200x50' or args.verify_only else list(dict.fromkeys(['rust033','v2',report['best_tier'],'python']))
    for effect in effects:
        row = batch['results'].setdefault(effect,{'times_ms':{},'outputs':{},'load_start':os.getloadavg()})
        terminal = ['--seed','1','--frame-rate','0','--canvas-width',str(columns),'--canvas-height',str(rows),'--ignore-terminal-dimensions']
        commands = {key:[str(paths[key]),*terminal,'--virtual-clock',effect] for key in keys if key!='python'}
        commands['python'] = ([sys.executable,str(ROOT/'benchmarks/python_clock_cli.py')] if effect in ['matrix','thunderstorm'] else [sys.executable,'-m','terminaltexteffects']) + [*terminal,effect]
        def run(key,output):
            result = subprocess.run(commands[key],input=data,stdout=output,stderr=subprocess.PIPE,env=env,timeout=3600)
            if result.returncode:raise RuntimeError((size,effect,key,result.returncode,result.stderr.decode(errors='replace')))
            if result.stderr:raise RuntimeError((size,effect,key,'unexpected stderr',result.stderr.decode(errors='replace')))
        for key in keys:
            if key in row['outputs']:continue
            print(f'{size} {effect}: verify {key}',flush=True)
            with tempfile.TemporaryFile() as f:
                start=time.perf_counter();run(key,f);length=f.tell();f.seek(0)
                row['outputs'][key]={'length':length,'sha256':hashlib.file_digest(f,'sha256').hexdigest(),'verification_seconds':time.perf_counter()-start}
            if key!='python' and (row['outputs'][key]['length'],row['outputs'][key]['sha256'])!=(row['outputs']['rust033']['length'],row['outputs']['rust033']['sha256']):
                save();raise RuntimeError(('compiled output mismatch',size,effect,key,row['outputs']))
            save()
        if args.verify_only:continue
        if all(len(row['times_ms'].get(key,[]))>=args.repeats for key in keys):
            row['median_ms']={key:statistics.median(row['times_ms'][key][:args.repeats]) for key in keys}
            save();continue
        for repetition in range(args.warmups+args.repeats):
            await_quiet()
            order=keys[repetition%len(keys):]+keys[:repetition%len(keys)]
            if repetition%2:order=order[::-1]
            for key in order:
                samples=row['times_ms'].setdefault(key,[])
                if repetition>=args.warmups and len(samples)>repetition-args.warmups:continue
                started=time.perf_counter_ns();run(key,subprocess.DEVNULL);elapsed=(time.perf_counter_ns()-started)/1e6
                if repetition>=args.warmups:samples.append(elapsed)
                row['load_end']=os.getloadavg();save()
            print(f'{size} {effect}: repetition {repetition+1}/{args.warmups+args.repeats}',flush=True)
        row['median_ms']={key:statistics.median(row['times_ms'][key][:args.repeats]) for key in keys}
        save();tables()
        print(size,effect,row['median_ms'],flush=True)
    if size=='200x50' and not args.verify_only:
        report['tier_geometric_mean_vs_rust033']={tier:math.exp(statistics.mean(math.log(row['median_ms']['rust033']/row['median_ms'][tier]) for row in batch['results'].values())) for tier in ['v2','v3','v4']}
        report['best_tier']=max(report['tier_geometric_mean_vs_rust033'],key=report['tier_geometric_mean_vs_rust033'].get)
        save();tables()
report['updated_utc']=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime());save();tables()
