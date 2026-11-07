import os
KLASOR=os.path.dirname(os.path.abspath(__file__))
# Yakıt, soğutma, yenileme ve enerji artışı: C# koduna bakılmadan CSV ve formüllerden bağımsız hesap
import csv, math, itertools, re
V=os.path.join(KLASOR,'..','Veri','')
L=lambda fn: list(csv.DictReader(open(V+fn,encoding='utf-8-sig')))
P={r['id']:float(r['deger']) for r in L('parametreler.csv')}
M={r['id']:r for r in L('malzemeler.csv')}; M.update({r['id']:r for r in L('cati_zemin.csv')})
B={int(r['bolge']):r for r in L('ts825_bolgeler.csv')}; IL={r['il']:r for r in L('iller.csv')}
YK={r['id']:r for r in L('yakitlar.csv')}; PT=L('ts825_pencere.csv')
f=lambda x: float(x)
def UP(c):
    ad=M[c]['ad'].lower(); tip='Üçlü, iki camı Low-E kaplamalı' if 'üçlü' in ad else 'Çift camlı Low-E kaplamalı'
    l=[(int(r['ara_bosluk_mm']),f(r['U_pencere_W_m2K'])) for r in PT if r['dograma']=='PVC Doğrama (5 Odacıklı)' and r['cam_tipi']==tip]
    m=re.search(r'\d+\+(\d+)\+\d+',M[c]['ad']); return dict(l)[int(m.group(1))] if m else max(u for _,u in l)
duvar,cati,yuk=294.0,110.25,7.0; kull=cati*2; vol=cati*yuk; cam=0.25*kull; opak=duvar-cam
Y=[k for k in M if k[0]=='Y']; C=[k for k in M if k[0]=='P']; D=[k for k in M if k[0]=='D']
def yangin(y):
    if 'eps' not in M[y]['ad'].lower(): return 0
    a1=[k for k in Y if 'taş' in M[k]['ad'].lower() and abs(f(M[k]['kalinlik_m'])-f(M[y]['kalinlik_m']))<1e-9][0]
    return 1.5*duvar/yuk*max(0,f(M[a1]['fiyat_TL_m2'])-f(M[y]['fiyat_TL_m2']))
fark={'n':0,'oneri':0,'lcc':0.0,'gider':0.0,'co2':0.0,'mg':0.0}; N=0
for row in csv.DictReader(open(os.path.join(KLASOR,'cs_yeni.csv'))):
    N+=1; il=IL[row['il']]; DG=f(il['isitma_derece_gun']); SDG=f(il['sogutma_derece_gun'])
    b=B[[int(x['ts825_bolge']) for x in L('il_bolgeleri.csv') if x['il']==row['il']][0]]
    k=int(row['kademe']); sog=row['sogutma']=='True'; mev=row['mevcut']=='True'; e=f(row['artis'])
    el=P['EL2' if k==2 else 'EL1']
    if row['yakit']=='DG': tl=P['E2' if k==2 else 'E1']/(P['E3']*0.001163); eta=P['H1']; em=P['E8']*0.0036
    else:
        y=YK[row['yakit']]; tl= el if y['birim']=='kWh' else f(y['fiyat_TL_birim'])/f(y['isil_deger_kWh_birim']); eta=f(y['verim']); em=f(y['emisyon_kgCO2_kWh'])
    UC=lambda c: 1/(1/P['M3']+f(M[c]['kalinlik_m'])/f(M[c]['lambda_W_mK']))
    uy=[c for c in ('C1','C2') if UC(c)<=f(b['U_cati'])+1e-12]; ck=min(uy,key=lambda c:f(M[c]['fiyat_TL_m2'])) if uy else min(('C1','C2'),key=UC); uc=UC(ck)
    uz=1/(1/P['M2']+0.1/0.035)
    hv=0.33*0.8*vol; sabit=0.8*uc*cati+0.5*uz*cati+hv; sy=(f(M[ck]['fiyat_TL_m2'])+f(M['Z1']['fiyat_TL_m2']))*cati
    r=(P['I1']-P['I2'])/(1+P['I2']); re_=(1+r)/(1+e)-1; pwf=(1-(1+re_)**-20)/re_
    def gider(H):
        q=0.024*DG*H; s=0.024*SDG*H/P['S1'] if sog else 0
        return q/eta*tl+s*el, (q/eta*em+s*P['EL3'])*20
    res=[]
    for y,c,d in itertools.product(Y,C,D[:1] if mev else D):
        uw=1/(1/P['M1']+f(M[y]['kalinlik_m'])/f(M[y]['lambda_W_mK'])) if mev else 1/(0.17+f(M[d]['kalinlik_m'])/f(M[d]['lambda_W_mK'])+f(M[y]['kalinlik_m'])/f(M[y]['lambda_W_mK']))
        up=UP(c); H=uw*opak+up*cam+sabit; g,co2=gider(H)
        inv=opak*((0 if mev else f(M[d]['fiyat_TL_m2']))+f(M[y]['fiyat_TL_m2']))+cam*f(M[c]['fiyat_TL_m2'])+sy+yangin(y)
        co2+=opak*((0 if mev else f(M[d]['karbon_kgCO2e_m2']))+f(M[y]['karbon_kgCO2e_m2']))+cam*f(M[c]['karbon_kgCO2e_m2'])
        ok=uw<=f(b['U_duvar']) and up<=f(b['U_pencere']) and uc<=f(b['U_cati']) and uz<=f(b['U_zemin'])
        res.append(dict(k=y+c+d,q=0.024*DG*H,lcc=inv+pwf*g,g=g,co2=co2,ok=ok))
    l=[x for x in res if x['ok']]
    fark['n']+=len(l)!=int(row['n'])
    if mev:
        HB=P['M1']*opak+P['M4']*cam+0.8*P['M3']*cati+0.5*P['M2']*cati+hv
        fark['mg']=max(fark['mg'],abs(gider(HB)[0]-f(row['mevcut_gider'])))
    if not l: continue
    w=[0.5,0.25,0.25]; kr=['lcc','q','co2']; nn=[math.sqrt(sum(x[k]**2 for x in l)) for k in kr]
    Vm=[[w[j]*x[k]/nn[j] for j,k in enumerate(kr)] for x in l]; bb=[min(v[j] for v in Vm) for j in range(3)]; wo=[max(v[j] for v in Vm) for j in range(3)]
    for x,v in zip(l,Vm): dp=math.dist(v,bb); dm=math.dist(v,wo); x['s']=1 if dp+dm==0 else dm/(dp+dm)
    top=max(l,key=lambda x:x['s'])
    fark['oneri']+=top['k']!=row['oneri']
    fark['lcc']=max(fark['lcc'],abs(top['lcc']-f(row['lcc']))); fark['gider']=max(fark['gider'],abs(top['g']-f(row['gider']))); fark['co2']=max(fark['co2'],abs(top['co2']-f(row['co2'])))
print('senaryo',N,fark)
