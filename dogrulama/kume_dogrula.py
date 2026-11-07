import os
KLASOR=os.path.dirname(os.path.abspath(__file__))
import csv, numpy as np
from sklearn.cluster import KMeans
from sklearn.metrics import silhouette_score, adjusted_rand_score
R=list(csv.DictReader(open(os.path.join(KLASOR,'..','Veri','iller.csv'),encoding='utf-8-sig')))
X=np.array([[float(r['isitma_derece_gun']),float(r['sogutma_derece_gun'])] for r in R])
Z=(X-X.mean(0))/X.std(0)   # nüfus standart sapması (C# ile aynı)
C={int(r['k']):r for r in csv.DictReader(open(os.path.join(KLASOR,'cs_kume.csv')))}
print("k | C# WCSS | sklearn WCSS | C# siluet | sklearn siluet (C# etiketleri) | sklearn siluet (kendi) | ARI")
for k in range(2,9):
    km=KMeans(n_clusters=k,init='k-means++',n_init=200,random_state=0).fit(Z)
    et=np.array([int(x) for x in C[k]['etiketler'].split('|')])
    s_cs=silhouette_score(Z,et); s_sk=silhouette_score(Z,km.labels_)
    print(k, round(float(C[k]['wcss']),4), round(km.inertia_,4), round(float(C[k]['siluet']),4), round(s_cs,4), round(s_sk,4), round(adjusted_rand_score(et,km.labels_),3))
from sklearn.metrics import davies_bouldin_score
print("k | C# DB | sklearn DB (C# etiketleri)")
for k in range(2,9):
    et=np.array([int(x) for x in C[k]['etiketler'].split('|')]); print(k, round(float(C[k]['db']),4), round(davies_bouldin_score(Z,et),4))
