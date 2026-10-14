"""Podgląd mgły „chibi” (port logiki z Assets/_Project/Shaders/Fog.shader): bulwiaste kłęby z obrysem i płaskimi kolorami.
Uruchom: python3 fog_chibi_preview.py  ->  chibi_band.png (przezroczyste okno), chibi_normal.png (zwykłe okno).
"""
import numpy as np, math, sys
from PIL import Image
W,H=240,180
yy,xx=np.mgrid[0:H,0:W].astype(float)
def dist(x,y):
    dx1=np.maximum(abs(x-120)-45,0); dy1=np.maximum(abs(y-95)-30,0); d1=np.hypot(dx1,dy1)
    dx2=np.maximum(abs(x-92)-20,0); dy2=np.maximum(abs(y-58)-18,0); d2=np.hypot(dx2,dy2)
    return np.minimum(d1,d2)
d=dist(xx,yy); land=d==0
def h4(cx,cy):
    p=np.stack([cx*0.1031,cy*0.1030,cx*0.0973,cy*0.1099],-1); p=p-np.floor(p)
    p=p+ (p*p[...,[3,2,0,1]]).sum(-1,keepdims=True)+33.33*0 + (p*(p[...,[3,2,0,1]]+33.33)).sum(-1,keepdims=True)*0
    p4=p
    s=(p4*(p4[...,[3,2,0,1]]+33.33)).sum(-1,keepdims=True)
    p4=p4+s
    r=(p4[...,[0,0,1,2]]+p4[...,[1,2,2,3]])*p4[...,[2,1,3,0]]
    return r-np.floor(r)
def sdf(px,py,mode,FO,T):
    CELL=11.0
    if mode=='band':
        base=d_at(px,py)-(FO+5); lo,hi=FO+2,FO+13
    else:
        base=(FO+14)-d_at(px,py); lo,hi=FO,FO+12
    s=base.copy()
    gx=np.floor(px/CELL); gy=np.floor(py/CELL)
    for i in (-1,0,1):
        for j in (-1,0,1):
            cx=gx+i; cy=gy+j; h=h4(cx,cy)
            ccx=(cx+0.2+0.6*h[...,0])*CELL+np.round(np.sin(T+h[...,2]*6.28)*1.5)
            ccy=(cy+0.2+0.6*h[...,1])*CELL
            r=4.5+4.0*h[...,2]+0.7*np.sin(T*1.7+h[...,3]*6.28)
            dc=d_at(ccx,ccy)
            ok=(dc>=lo)&(dc<=hi)
            c=np.hypot(px-ccx,py-ccy)-r
            s=np.where(ok,np.minimum(s,c),s)
    return s
def d_at(x,y): return dist(x,y)
def render(mode,T,FO,fn):
    s0=sdf(xx,yy,mode,FO,T)
    sh=sdf(xx+2,yy+2,mode,FO,T)   # przesunięcie w dół-prawo (y w dół)
    hi=sdf(xx-1,yy-2,mode,FO,T)
    inside=s0<0
    if mode=='band': inside&= d>FO-3
    out=(s0>=0)&(s0<1.2)
    if mode=='band': out&= d>FO-1
    col=np.zeros((H,W,3)); a=np.zeros((H,W))
    base=np.array([132,142,155.]); shadow=np.array([86,105,122.]); light=np.array([196,202,212.]); line=np.array([40,52,70.])
    col[inside]=base; a[inside]=0.94
    m=inside&(sh>=0); col[m]=shadow
    m=inside&(hi>=0)&~(sh>=0); col[m]=light
    col[out]=line; a[out]=0.94
    img=np.full((H,W,3),[60,60,64.]) if mode=='band' else np.full((H,W,3),[31,48,68.])
    img[land]=[52,74,59]
    if mode=='band':
        water=(d>0)&(d<FO); img[water]=[31,48,68]
    img=img*(1-a[...,None])+col*a[...,None]
    Image.fromarray(img.astype(np.uint8)).resize((W*3,H*3),Image.NEAREST).save(fn)
render('band',1.0,18,'chibi_band.png')
render('normal',1.0,22,'chibi_normal.png')
