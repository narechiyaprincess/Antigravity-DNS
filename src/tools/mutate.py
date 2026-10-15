# usage: python src/tools/mutate.py spec.json [parallel]
# spec: [{"name":..., "file":"Connect.cs", "old":"...", "new":"..."}]  (file relative to src/app/; several edits: "edits":[{file,old,new},...]; "tier": E2E tier, default 8)
# Each mutant: a copy of src/app and src/tests in src/.work/mut/<name>, the edit, a build, the self-test and the tier. Logs: src/.work/logs/mutants.
import json, sys, os, shutil, subprocess, re, concurrent.futures as cf
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # src
dn = os.environ.get('AGDNS_DOTNET') or 'dotnet'
logs = os.path.join(root, '.work', 'logs', 'mutants')
os.makedirs(logs, exist_ok=True)
def run(m):
    name = m['name']; d = os.path.join(root, '.work', 'mut', name)
    shutil.rmtree(d, ignore_errors=True); os.makedirs(d)
    tier = m.get('tier', '8')
    shutil.copytree(os.path.join(root, 'app'), os.path.join(d, 'app'))
    # Historical script fixtures are used by tiers 1-4.
    big = tier in ('1', '2', '3', '4', 'All')
    shutil.copytree(os.path.join(root, 'tests'), os.path.join(d, 'tests'), ignore=None if big else shutil.ignore_patterns('baseline-1.1.0'))
    edits = m.get('edits') or [m]
    for e in edits:
        fp = os.path.join(d, 'app', e['file'])
        t = open(fp, encoding='utf-8-sig', newline='').read()
        if t.count(e['old']) != 1:
            return (name, 'EDIT-NOT-UNIQUE(%d): %s' % (t.count(e['old']), e['old'][:60]), '', '')
        open(fp, 'w', encoding='utf-8-sig', newline='').write(t.replace(e['old'], e['new']))
    out = os.path.join(d, 'out')
    b = subprocess.run(['powershell.exe', '-NoProfile', '-File', os.path.join(d, 'app', 'Build.ps1'), '-Dotnet', dn, '-OutputDirectory', out], capture_output=True, text=True)
    open(os.path.join(logs, name + '.build.txt'), 'w', encoding='utf-8').write(b.stdout + b.stderr)
    exe = os.path.join(out, 'AntigravityDnsUnlocker.exe')
    if b.returncode != 0 or not os.path.exists(exe):
        return (name, 'BUILD-FAILED', '', '')
    try:
        st = subprocess.run([exe, '--self-test'], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=150)
        fail = [l for l in (st.stdout + st.stderr).splitlines() if 'FAIL' in l][:2]
        selftest = 'SELFTEST-RED: ' + ' | '.join(fail) if st.returncode != 0 else 'selftest-green'
    except subprocess.TimeoutExpired:
        selftest = 'SELFTEST-RED: TIMEOUT(hang)'
    env = dict(os.environ, AGDNS_EXE=exe)
    t8 = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', os.path.join(d, 'tests', 'run_offline_e2e_tests.ps1'), '-Tier', tier], capture_output=True, text=True, encoding='utf-8', errors='replace', env=env)
    txt = t8.stdout
    open(os.path.join(logs, name + '.t8.txt'), 'w', encoding='utf-8').write(txt)
    reds = re.findall(r'\[FAIL\] (\S+)', txt)
    return (name, 'ok', selftest, 'T8-RED: ' + ','.join(reds) if reds else 't8-green')
if __name__ == '__main__':
    spec = json.load(open(sys.argv[1], encoding='utf-8'))
    par = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    with cf.ThreadPoolExecutor(par) as ex:
        res = list(ex.map(run, spec))
    lines = ['%-14s %s | %s | %s' % r for r in res]
    open(os.path.join(logs, 'summary_%s.txt' % os.path.basename(sys.argv[1])), 'w', encoding='utf-8').write('\n'.join(lines))
    print('\n'.join(lines))
