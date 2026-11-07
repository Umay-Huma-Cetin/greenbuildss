import os
KLASOR=os.path.dirname(os.path.abspath(__file__))
import csv, math, itertools
V=os.path.join(KLASOR,'..','Veri','')
P={}
for r in csv.DictReader(open(V+'parametreler.csv',encoding='utf-8-sig')):
    try: P[r['id']]=float(r['deger'])
    except: pass
P['E4']=P['E3']*0.001163; P['E9']=P['E8']*0.0036
P['T2'],P['T3'],P['T4'],P['T5']=0.35,0.25,0.30,1.8
M={r['id']:r for r in csv.DictReader(open(V+'malzemeler.csv',encoding='utf-8-sig'))}
M.update({r['id']:r for r in csv.DictReader(open(V+'cati_zemin.csv',encoding='utf-8-sig'))})
B={int(r['bolge']):r for r in csv.DictReader(open(V+'ts825_bolgeler.csv',encoding='utf-8-sig'))}
IL={r['il']:float(r['isitma_derece_gun']) for r in csv.DictReader(open(V+'iller.csv',encoding='utf-8-sig'))}
LVA=list(csv.DictReader(open(V+'levha_fiyatlari.csv',encoding='utf-8-sig'))); LV=lambda a:[(float(r['kalinlik_cm']),float(r['fiyat_TL_m2'])) for r in LVA if r['aile']==a]
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
def ols(x,y):
    mx,my=sum(x)/len(x),sum(y)/len(y); b=sum((a-mx)*(c-my) for a,c in zip(x,y))/sum((a-mx)**2 for a in x); return my-b*mx,b
slope={k:ols([c for c,_ in LV(a)],[p for _,p in LV(a)])[1] for k,a in (('EPS','EPS'),('TY','Taş yünü'))}
lam={'EPS':0.04,'TY':0.035}
duvar,cati,yuk=294.0,110.25,7.0; kull=cati*2; vol=cati*yuk; cam=0.25*kull; opak=duvar-cam
uz=1/(1/P['M2']+0.1/0.035)
CK=[k for k in M if k[0]=='C']; UC=lambda k:1/(1/P['M3']+f(M[k]['kalinlik_m'])/f(M[k]['lambda_W_mK']))
r_=(P['I1']-P['I2'])/(1+P['I2']); pwf=(1-(1+r_)**-20)/r_; gaz=P['E1']/P['E4']
Y=[k for k in M if k[0]=='Y'];C_=[k for k in M if k[0]=='P'];D=[k for k in M if k[0]=='D']
fark={'n':0,'oneri':0,'opt':0.0,'min':0.0,'lcc':0.0}; N=0
for row in csv.DictReader(open(os.path.join(KLASOR,'cs_il.csv'))):
    N+=1; DG=IL[row['il']]; b=B[int(row['bolge'])]
    uy=[k for k in CK if UC(k)<=float(b['U_cati'])+1e-12]; ck=min(uy,key=lambda k:f(M[k]['fiyat_TL_m2'])) if uy else min(CK,key=UC); uc=UC(ck)
    sabit=0.8*uc*cati+0.5*uz*cati+0.33*0.8*vol; sy=(f(M[ck]['fiyat_TL_m2'])+f(M['Z1']['fiyat_TL_m2']))*cati
    res=[]
    for y,c,d in itertools.product(Y,C_,D):
        up=UP('PVC Doğrama (5 Odacıklı)',c)
        if up is None: continue
        uw=1/(0.17+f(M[d]['kalinlik_m'])/f(M[d]['lambda_W_mK'])+f(M[y]['kalinlik_m'])/f(M[y]['lambda_W_mK']))
        H=uw*opak+up*cam+sabit; Q=0.024*DG*H; g=Q/P['H1']
        inv=opak*(f(M[d]['fiyat_TL_m2'])+f(M[y]['fiyat_TL_m2']))+cam*f(M[c]['fiyat_TL_m2'])+sy+yangin(y,duvar,yuk)
        co2=opak*(f(M[d]['karbon_kgCO2e_m2'])+f(M[y]['karbon_kgCO2e_m2']))+cam*f(M[c]['karbon_kgCO2e_m2'])+g*P['E9']*20
        ok=uw<=float(b['U_duvar']) and up<=float(b['U_pencere']) and uc<=float(b['U_cati']) and uz<=float(b['U_zemin'])
        res.append(dict(k=y+c+d,d=d,c=c,Q=Q,lcc=inv+pwf*g*gaz,co2=co2,ok=ok))
    l=[x for x in res if x['ok']]
    fark['n']+= len(l)!=int(row['n'])
    if l:
        w=[0.5,0.25,0.25]; kr=['lcc','Q','co2']; nn=[math.sqrt(sum(x[k]**2 for x in l)) for k in kr]
        Vm=[[w[j]*x[k]/nn[j] for j,k in enumerate(kr)] for x in l]; bb=[min(v[j] for v in Vm) for j in range(3)]; wo=[max(v[j] for v in Vm) for j in range(3)]
        for x,v in zip(l,Vm): dp=math.dist(v,bb); dm=math.dist(v,wo); x['s']=dm/(dp+dm)
        top=max(l,key=lambda x:x['s']); fark['oneri']+= top['k']!=row['oneri']; fark['lcc']=max(fark['lcc'],abs(top['lcc']-float(row['lcc'])))
        dd=top['d']
    else:
        dd=min(res,key=lambda x:x['lcc'])['d']
    R0=0.17+f(M[dd]['kalinlik_m'])/f(M[dd]['lambda_W_mK'])
    for key,col in [('EPS','epsopt'),('TY','tyopt')]:
        dst=max(0,(math.sqrt(pwf*gaz*0.024*DG*lam[key]/(P['H1']*slope[key]*100))-lam[key]*R0)*100)
        fark['opt']=max(fark['opt'],abs(dst-float(row[col])))
    fark['min']=max(fark['min'],abs(max(0,0.04*(1/float(b['U_duvar'])-R0)*100)-float(row['epsmin'])))
print('senaryo',N,fark)
