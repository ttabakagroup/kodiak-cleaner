"""Check every theme in theme.css against WCAG 2.2 AA (also covers WCAG 2.1 AA, the level ADA rules reference).
Usage: python3 check_contrast.py path/to/theme.css   (exits 1 if anything fails)
Text pairs need 4.5:1. Controls, borders that identify a control, focus rings and toggle states need 3:1.
Translucent tokens (accent-tint) are measured on top of --surface.
Also checks every accent swatch in accent.js (found next to theme.css) plus sample Windows accents,
in every theme and mode. Needs Node.js for the accent check."""
import re,json,sys,os,subprocess
def parse_css(path):
    css=open(path).read()
    blocks={}
    def grab(sel_regex):
        m=re.search(sel_regex+r'\s*\{([^}]*)\}',css)
        return dict(re.findall(r'--([\w-]+):\s*([^;]+);',m.group(1)))
    base=grab(r'/\* Honey light \(default\) \*/\s*:root')
    hd=grab(r':root\[data-theme="dark"\]')
    cl=grab(r':root\[data-palette="classic"\]')
    cd=grab(r':root\[data-palette="classic"\]\[data-theme="dark"\]')
    gl=grab(r':root\[data-palette="glacier"\]')
    gd=grab(r':root\[data-palette="glacier"\]\[data-theme="dark"\]')
    ll=grab(r':root\[data-palette="lagoon"\]')
    ld=grab(r':root\[data-palette="lagoon"\]\[data-theme="dark"\]')
    T={'honey-light':dict(base),'honey-dark':{**base,**hd},'classic-light':{**base,**cl},'classic-dark':{**base,**cl,**cd},'glacier-light':{**base,**gl},'glacier-dark':{**base,**gl,**gd},'lagoon-light':{**base,**ll},'lagoon-dark':{**base,**ll,**ld}}
    for k,t in T.items():
        for _ in range(3):
            for n,v in list(t.items()):
                m=re.fullmatch(r'var\(--([\w-]+)\)',v.strip())
                if m: t[n]=t[m.group(1)]
    return T
def rgb(v):
    v=v.strip()
    if v.startswith('#'):
        h=v[1:]; return tuple(int(h[i:i+2],16) for i in (0,2,4)),1.0
    m=re.match(r'rgba\(([\d.]+),([\d.]+),([\d.]+),([\d.]+)\)',v.replace(' ',''))
    return tuple(float(m.group(i)) for i in (1,2,3)),float(m.group(4))
def over(fg,bg):
    (c,a),(b,_)=rgb(fg),rgb(bg)
    return tuple(round(c[i]*a+b[i]*(1-a)) for i in range(3))
def lum(c):
    c=[x/255 for x in c]; c=[x/12.92 if x<=0.03928 else ((x+0.055)/1.055)**2.4 for x in c]
    return 0.2126*c[0]+0.7152*c[1]+0.0722*c[2]
def cr(a,b):
    a,b=lum(a),lum(b); return (max(a,b)+.05)/(min(a,b)+.05)
TEXT=[('text','bg'),('text','surface'),('text','surface-alt'),('text','row-alt'),('text','row-hover'),('text','tint@surface'),
('muted','bg'),('muted','surface'),('muted','surface-alt'),('muted','row-alt'),
('link','bg'),('link','surface'),('link','surface-alt'),
('accent-text','bg'),('accent-text','surface'),('accent-text','tint@surface'),
('on-primary','primary'),('danger','bg'),('danger','surface'),('danger-fg','danger-bg'),
('warn-text','warn-bg'),('chip-text','chip-bg')]
UI=[('control-border','surface'),('control-border','surface-alt'),('control-border','bg'),
('focus-ring','bg'),('focus-ring','surface'),('focus-ring','surface-alt'),
('toggle-on','surface'),('toggle-on','surface-alt'),('surface','toggle-on'),
('success','surface'),('on-success','success')]
def col(t,n):
    if n=='tint@surface': return over(t['accent-tint'],t['surface'])
    if n=='focus-ring' and 'focus-ring' not in t: n='accent'
    c,a=rgb(t[n])
    return c if a==1 else over(t[n],t['surface'])
WINDOWS_SAMPLES=['#0078D4','#D0000C','#FFB900','#FFC0CB','#E3008C','#00B7C3','#111111','#FFFFFF','#767676']
def hexof(t,n):
    c=col(t,n); return '#%02X%02X%02X'%tuple(int(x) for x in c)
def accent_sets(path,T):
    js=os.path.join(os.path.dirname(os.path.abspath(path)),'accent.js')
    if not os.path.exists(js): print('accent.js not found next to theme.css: accent check skipped'); return {}
    themes={k:{'bg':hexof(t,'bg'),'surface':hexof(t,'surface'),'surfaceAlt':hexof(t,'surface-alt'),'text':hexof(t,'text'),'dark':k.endswith('dark')} for k,t in T.items()}
    code=("const a=require(process.argv[1]);const th=JSON.parse(process.argv[2]);const w=JSON.parse(process.argv[3]);"
          "const bases=[...a.ACCENT_SWATCHES.map(s=>[s.name,s.hex]),...w.map(h=>['Windows '+h,h])];const out={};"
          "for(const[k,t]of Object.entries(th))for(const[n,h]of bases)out[k+' + '+n]=[k,a.accentTokens(h,t)];"
          "console.log(JSON.stringify(out));")
    r=subprocess.run(['node','-e',code,js,json.dumps(themes),json.dumps(WINDOWS_SAMPLES)],capture_output=True,text=True,check=True)
    return json.loads(r.stdout)
def check(name,t):
    out=[]
    for lst,need,kind in ((TEXT,4.5,'text'),(UI,3.0,'ui')):
        for a,b in lst:
            r=cr(col(t,a),col(t,b))
            if r<need: out.append(f'   FAIL {kind} {a} on {b}: {r:.2f} (need {need})')
    if out or not name.count('+'): print(name,'OK' if not out else ''); [print(o) for o in out]
    return len(out)
def run(path):
    T=parse_css(path); bad=0
    for k,t in T.items(): bad+=check(k,t)
    acc=accent_sets(path,T)
    for name,(k,tokens) in acc.items():
        t=dict(T[k]); t.update({n.lstrip('-'):v for n,v in tokens.items()})
        if not k.startswith('classic'): t['row-hover']=t['accent-tint']  # Honey/Lagoon/Glacier row hover follows the accent tint
        bad+=check(name,t)
    if acc: print(f'{len(acc)} accent combinations checked')
    print('All themes pass WCAG 2.2 AA' if not bad else f'{bad} failing pair(s)')
    return bad
if __name__=='__main__': sys.exit(1 if run(sys.argv[1]) else 0)
