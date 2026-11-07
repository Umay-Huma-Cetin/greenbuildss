import os
KLASOR=os.path.dirname(os.path.abspath(__file__))
# C# karar ağacının (Core/Siniflandirma.cs) scikit-learn ile bağımsız karşılaştırması
import csv, numpy as np
from sklearn.tree import DecisionTreeClassifier, export_text
from sklearn.model_selection import LeaveOneOut, cross_val_predict
R=list(csv.DictReader(open(os.path.join(KLASOR,'cs_sinif.csv'),encoding='utf-8')))
X=np.array([[float(r['isitma']),float(r['sogutma']),float(r['bolge'])] for r in R]); y=np.array([r['etiket'] for r in R])
cs=np.array([r['loocv'] for r in R])
print("n",len(y),"sınıflar",{c:int((y==c).sum()) for c in sorted(set(y))})
print("taban (en sık sınıf)", round(max((y==c).mean() for c in set(y)),4))
for d in range(1,7):
    accs=[]
    for rs in range(20):
        p=cross_val_predict(DecisionTreeClassifier(criterion='gini',max_depth=d,random_state=rs),X,y,cv=LeaveOneOut())
        accs.append((p==y).mean())
    print("derinlik",d,"LOOCV sklearn (20 tohum) min",round(min(accs),4),"max",round(max(accs),4))
p=cross_val_predict(DecisionTreeClassifier(criterion='gini',max_depth=3,random_state=0),X,y,cv=LeaveOneOut())
print("d=3 C# ile aynı LOOCV tahmini:", int((p==cs).sum()),"/",len(y))
m=DecisionTreeClassifier(criterion='gini',max_depth=3,random_state=0).fit(X,y)
print("eğitim doğruluğu",(m.predict(X)==y).mean()); print("önem",m.feature_importances_.round(3))
print(export_text(m,feature_names=['isitma','sogutma','bolge']))
# Eşit kazançlı bölünmeler: scikit-learn eşitlikte özellikleri rastgele sırayla dener. C# ağacıyla aynı ağacı veren tohum var mı?
from sklearn.tree import _tree
def kurallar(m):
    t=m.tree_; out=[]
    def g(i):
        if t.feature[i]==_tree.TREE_UNDEFINED: out.append(('yaprak',m.classes_[t.value[i][0].argmax()])); return
        out.append((int(t.feature[i]),round(float(t.threshold[i]),1))); g(t.children_left[i]); g(t.children_right[i])
    g(0); return out
cs_agac=[(0,2054.0),('yaprak','Y2P3D3'),(0,3064.5),(0,2248.0),('yaprak','Y3P3D3'),('yaprak','Y7P3D3'),('yaprak','Y8P3D3')]
ayni=[rs for rs in range(50) if kurallar(DecisionTreeClassifier(criterion='gini',max_depth=3,random_state=rs).fit(X,y))==cs_agac]
print("C# ağacıyla birebir aynı ağaç veren scikit-learn tohumları:", ayni[:10], "(50 tohumdan", len(ayni), ")")
for rs in ayni[:1]:
    p=cross_val_predict(DecisionTreeClassifier(criterion='gini',max_depth=3,random_state=rs),X,y,cv=LeaveOneOut())
    print("o tohumda LOOCV", (p==y).mean().round(4), "· C# LOOCV tahminiyle aynı:", int((p==cs).sum()), "/", len(y))
