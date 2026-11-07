import os
KLASOR=os.path.dirname(os.path.abspath(__file__))
import csv, math, itertools
V=os.path.join(KLASOR,'..','Veri','')
P={}
for r in csv.DictReader(open(V+'parametreler.csv',encoding='utf-8-sig')):
    try: P[r['id']]=float(r['deger'])
    except: pass
P['E4']=P['E3']*0.001163; P['E9']=P['E8']*0.0036; P['K1']=2425.0; P['I3']=(P['I1']-P['I2'])/(1+P['I2'])
P['T2'],P['T3'],P['T4'],P['T5']=0.35,0.25,0.30,1.8
M={r['id']:r for r in csv.DictReader(open(V+'malzemeler.csv',encoding='utf-8-sig'))}
M.update({r['id']:r for r in csv.DictReader(open(V+'cati_zemin.csv',encoding='utf-8-sig'))})
f=lambda x: float(x) if x else 0.0
def yangin(y,duvar_alan,yuk):
    # Md. 27/2-b: EPS (zor alevlenici sistem) için zeminden 1,5 m A1 şerit; fiyat farkı = şerit × (aynı kalınlıkta taş yünü − EPS)
    if 'eps' not in M[y]['ad'].lower(): return 0.0
    a1=[k for k in M if k[0]=='Y' and 'taş' in M[k]['ad'].lower() and abs(f(M[k]['kalinlik_m'])-f(M[y]['kalinlik_m']))<1e-9]
    if not a1: return 0.0
    serit=min(1.5,yuk)*duvar_alan/yuk
    return serit*max(0,f(M[a1[0]]['fiyat_TL_m2'])-f(M[y]['fiyat_TL_m2']))
import re
PT=list(csv.DictReader(open(V+'ts825_pencere.csv',encoding='utf-8-sig')))
def UP(dog,c):
    ad=M[c]['ad'].lower()
    if 'low-e' not in ad: return None
    tip='Üçlü, iki camı Low-E kaplamalı' if 'üçlü' in ad else 'Çift camlı Low-E kaplamalı'
    l=[(int(r['ara_bosluk_mm']),float(r['U_pencere_W_m2K'])) for r in PT if r['dograma']==dog and r['cam_tipi']==tip]
    if not l: return None
    m=re.search(r'\d+\+(\d+)\+\d+',M[c]['ad'])
    return dict(l).get(int(m.group(1))) if m else max(u for _,u in l)
Y=[k for k in M if k[0]=='Y']; C=[k for k in M if k[0]=='P']; D=[k for k in M if k[0]=='D']
def ahp(a12,a13,a23):
    A=[[1,a12,a13],[1/a12,1,a23],[1/a13,1/a23,1]]; g=[(r[0]*r[1]*r[2])**(1/3) for r in A]; s=sum(g); return [x/s for x in g]
ON=[(5,5,1),(2,2,1),(1/3,1/3,1)]
fam={'EPS':['Y1','Y2','Y3'],'TY':['Y4','Y5','Y6']}
def reg(ids):
    xs=[f(M[i]['kalinlik_m'])*100 for i in ids]; ys=[f(M[i]['fiyat_TL_m2']) for i in ids]
    mx,my=sum(xs)/3,sum(ys)/3; b=sum((a-mx)*(c-my) for a,c in zip(xs,ys))/sum((a-mx)**2 for a in xs); return my-b*mx,b,f(M[ids[0]]['lambda_W_mK'])
REG={k:reg(v) for k,v in fam.items()}
def levha(aile):
    LV=[(float(r['kalinlik_cm']),float(r['fiyat_TL_m2'])) for r in csv.DictReader(open(V+'levha_fiyatlari.csv',encoding='utf-8-sig')) if r['aile']==aile]
    mx_=sum(c for c,_ in LV)/len(LV); my_=sum(p for _,p in LV)/len(LV); return sum((c-mx_)*(p-my_) for c,p in LV)/sum((c-mx_)**2 for c,_ in LV)
for k,aile in (('TY','Taş yünü'),('EPS','EPS')):   # ek cm maliyeti levha serisinden; doğru uygulanmış fiyatların ağırlık merkezinden geçer
    ids=fam[k]; xs=[f(M[i]['kalinlik_m'])*100 for i in ids]; ys=[f(M[i]['fiyat_TL_m2']) for i in ids]; b=levha(aile)
    REG[k]=(sum(ys)/3-b*sum(xs)/3,b,REG[k][2])
print('egimler',{k:round(v[1],4) for k,v in REG.items()})
uc=1/(1/P['M3']+f(M['C1']['kalinlik_m'])/f(M['C1']['lambda_W_mK'])); uz=1/(1/P['M2']+f(M['Z1']['kalinlik_m'])/f(M['Z1']['lambda_W_mK']))
fark={'oneri':0,'eko':0,'yp':0,'n':0,'butce':0}; mx={'lcc':0,'inv':0,'q':0,'opt':0,'min':0}; N=0
for row in csv.DictReader(open(os.path.join(KLASOR,'cs_dump.csv'))):
    N+=1
    duvar,cati,yuk,cam_o=float(row['duvar']),float(row['cati']),float(row['yuk']),float(row['cam'])
    ozel=cati in (16.0,180.0,600.0)
    vol=cati*yuk if ozel else 771.75   # ENB tipleri: makaledeki ortak hacim
    kull=vol/P['M7']
    cam=cam_o*kull
    if cam>duvar: cam=duvar
    opak=max(0,duvar-cam); yil=int(row['yil'])
    gaz=(P['E1'] if row['tarife']=='0' else P['E2'])/P['E4']
    r=(P['I1']-P['I2'])/(1+P['I2']); pwf=(1-(1+r)**-yil)/r
    sabit=0.8*uc*cati+0.5*uz*cati+0.33*1.0*0.8*vol; sy=(f(M['C1']['fiyat_TL_m2'])+f(M['Z1']['fiyat_TL_m2']))*cati
    res=[]
    dog=row['dograma']
    for y,c,d in itertools.product(Y,C,D):
        if yuk>28.5 and 'eps' in M[y]['ad'].lower(): continue
        up=UP(dog,c)
        if up is None: continue
        uw=1/(0.17+f(M[d]['kalinlik_m'])/f(M[d]['lambda_W_mK'])+f(M[y]['kalinlik_m'])/f(M[y]['lambda_W_mK']))
        H=uw*opak+up*cam+sabit; Q=0.024*P['K1']*H; g=Q/P['H1']; gid=g*gaz
        inv=opak*(f(M[d]['fiyat_TL_m2'])+f(M[y]['fiyat_TL_m2']))+cam*f(M[c]['fiyat_TL_m2'])+sy+yangin(y,duvar,yuk)
        co2=opak*(f(M[d]['karbon_kgCO2e_m2'])+f(M[y]['karbon_kgCO2e_m2']))+cam*f(M[c]['karbon_kgCO2e_m2'])+g*P['E9']*yil
        ok=uw<=P['T2'] and (cam==0 or up<=P['T5']) and uc<=P['T3'] and uz<=P['T4']
        res.append(dict(k=y+c+d,y=y,c=c,d=d,uw=uw,Q=Q,inv=inv,lcc=inv+pwf*gid,co2=co2,ok=ok))
    l=[x for x in res if x['ok']] if row['filtre']=='True' else res
    if len(l)!=int(row['n']): fark['n']+=1
    if not l: continue
    w=ahp(*ON[int(row['onc'])]); kr=['lcc','Q','co2']
    nn=[math.sqrt(sum(x[k]**2 for x in l)) for k in kr]
    Vm=[[w[j]*x[k]/nn[j] for j,k in enumerate(kr)] for x in l]
    b=[min(v[j] for v in Vm) for j in range(3)]; wo=[max(v[j] for v in Vm) for j in range(3)]
    for x,v in zip(l,Vm): dp=math.dist(v,b); dm=math.dist(v,wo); x['s']=1 if dp+dm==0 else dm/(dp+dm)
    srt=sorted(l,key=lambda x:-x['s']); top=srt[0]
    eko=sorted(l,key=lambda x:(x['inv'],-x['s']))[0]; yp=sorted(l,key=lambda x:(x['Q'],x['inv']))[0]
    fark['oneri']+=top['k']!=row['oneri']; fark['eko']+=eko['k']!=row['eko']; fark['yp']+=yp['k']!=row['yp']
    mx['lcc']=max(mx['lcc'],abs(top['lcc']-float(row['oneri_lcc']))); mx['inv']=max(mx['inv'],abs(eko['inv']-float(row['eko_inv']))); mx['q']=max(mx['q'],abs(yp['Q']-float(row['yp_q'])))
    # optimum (kapalı form, 0'da kırpılmış) — duvar/cam = öneri
    R0=0.17+f(M[top['d']]['kalinlik_m'])/f(M[top['d']]['lambda_W_mK'])
    for key,colo,colm in [('EPS','optEPS','minEPS'),('TY','optTY','minTY')]:
        a0,bb,lam=REG[key]
        d=max(0,(math.sqrt(pwf*gaz*0.024*P['K1']*lam/(P['H1']*bb*100))-lam*R0)*100) if opak>0 else None
        if d is not None: mx['opt']=max(mx['opt'],abs(d-float(row[colo])))
        mx['min']=max(mx['min'],abs(max(0,lam*(1/P['T2']-R0)*100)-float(row[colm])))
    # bütçe = önerinin yatırımı
    allok=[x for x in res if x['ok']]
    for amac,col in [('Q','b_isitma'),('co2','b_co2'),('lcc','b_lcc')]:
        cand=[x for x in allok if x['inv']<=top['inv']]
        best=sorted(cand,key=lambda x:(x[amac],x['inv']))[0]['k'] if cand else ''
        fark['butce']+=best!=row[col]
print(f"senaryo {N}"); print("farklı sonuç sayısı:",fark); print("en büyük sayısal fark:",{k:f'{v:.2e}' for k,v in mx.items()})
