"""Podgląd efektów wody z GDD §9 p. 119 (port fragmentów Water.shader do numpy, w przybliżeniu):
od góry refleksy słońca na płyciźnie z wodorostami, kółka od deszczu, księżycowa ścieżka (noc), smugi wiatru.
Pas wody: odległość od brzegu rośnie w dół. Uruchom: python3 water_effects_preview.py -> docs/img/woda_efekty.png
"""
import os
import numpy as np
from PIL import Image
W,H=240,120
B=[(0x30,0x48,0x5f),(0x1f,0x30,0x44),(0x12,0x1c,0x27),(0x1c,0x15,0x20)]
B3=(0x56,0x69,0x7a); FO=(0x84,0x8e,0x9b); O=(0x0f,0x0a,0x10)
BAY=[0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5]
def fr(x): return x-np.floor(x)
def hash4(px,py):
    p=np.stack([fr(px*.1031),fr(py*.1030),fr(px*.0973),fr(py*.1099)],-1)
    dt=(p*(p[...,[3,2,0,1]]+33.33)).sum(-1,keepdims=True); p=p+dt
    a=(p[...,[0,0,1,2]]+p[...,[1,2,2,3]])*p[...,[2,1,3,0]]
    return fr(a)
def wnoise(x,y):
    ix,iy=np.floor(x),np.floor(y); fx,fy=x-ix,y-iy; fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy)
    a=hash4(ix,iy)[...,0]; b=hash4(ix+1,iy)[...,0]; c=hash4(ix,iy+1)[...,0]; e=hash4(ix+1,iy+1)[...,0]
    return (a+(b-a)*fx)*(1-fy)+(c+(e-c)*fx)*fy
Y,X=np.mgrid[0:H,0:W].astype(float)
d=Y/H*30+1   # odległość od brzegu rośnie w dół
th=np.array(BAY)[(Y.astype(int)&3)*4+(X.astype(int)&3)]/16
def base():
    cls=np.where(d<7,1,np.where(d<22,2,3))
    img=np.zeros((H,W,3)); 
    for k in (1,2,3): img[cls==k]=B[k-1]
    return img,cls
def light_of(cls): 
    out=np.zeros(cls.shape+(3,)); out[cls==1]=B3; out[cls==2]=B[0]; out[cls==3]=B[1]; return out
def sun(t,img,cls):
    m=(d>2)&(d<18)
    n1=wnoise(X/7+t*.35,Y/7+t*.2); n2=wnoise(X/6-t*.3+23,Y/6+t*.25+23)
    c=m&(np.abs(n1-n2)<.07)&(th<(1-d/18)*1.3)
    img[c]=light_of(cls)[c]
    # wodorosty
    for wc in (-1,0,1):
        wrow=np.floor(Y/14); wcol=np.floor(X/9)+wc
        h=hash4(wcol+51.3,wrow+51.3)
        bx=wcol*9+np.floor(h[...,1]*9); by=wrow*14+13-np.floor(h[...,2]*3); ln=4+np.floor(h[...,3]*5)
        k=np.floor(by-Y); sway=np.round(np.sin(t*1.4+h[...,3]*6.28+k*.45)*1.5*(k/ln))
        c=(d>5)&(d<22)&(h[...,0]<.3)&(k>=0)&(k<ln)&(np.floor(X)==bx+sway)
        img[c]=img[c]*.3+np.array([.13,.2,.17])*255*.7
def rain(t,img,cls,R=1):
    lt=light_of(cls)
    for ry in (-1,0,1):
        for rx in (-1,0,1):
            cx=np.floor(X/14)+rx; cy=np.floor(Y/9)+ry
            tt=t/.7+hash4(cx+3.7,cy+3.7)[...,3]; u=fr(tt)
            h=hash4(cx*1.31+np.floor(tt)*7.17,cy*1.31+np.floor(tt)*7.17)
            on=h[...,0]<R*.55
            ctrx=np.floor(cx*14+2+h[...,1]*10); ctry=np.floor(cy*9+2+h[...,2]*5)
            vx,vy=X-ctrx,Y-ctry; rr=.5+u*3.5; e=np.sqrt((vx/rr)**2+(vy/(rr*.5))**2)
            dot=on&(u<.15)&(np.abs(vx)<.5)&(np.abs(vy)<.5)
            ring=on&~dot&(u>=.1)&(np.abs(e-1)*rr*.5<.5)&(th>u*.9)&(d>2)
            img[dot]=B3; img[ring]=lt[ring]
def moon(t,img):
    wob=np.sin(Y*.27+t*.8)*1.5; mdx=(X*.5-60+wob)/9; mp=np.exp(-mdx**2)
    mcx=np.floor(X/3); h0=hash4(mcx,Y)[...,0]
    mh=hash4(mcx*1.73+np.floor(t*4+h0*4)*5.1,Y*1.73+np.floor(t*4+h0*4)*5.1)
    mx0=mcx*3+np.floor(mh[...,1]*2); ln=np.where(mh[...,2]<.4,2,1)
    c=(mh[...,0]<mp*.3)&(X>=mx0)&(X<mx0+ln)&(d>3)
    a=(.3+.35*mp)[...,None]
    img[:]=img*.35  # noc: woda ciemniejsza
    img[c]=(img*(1-a)+np.array([.8,.84,.88])*255*a)[c]
def wind(t,img,cls,g=.8):
    sn=wnoise((X-t*60)/34+71,Y/1.6+71)
    c=(d>4)&(sn>.76-.08*g)&(th<g*0.9)
    deeper=np.zeros_like(img); deeper[cls==1]=B[1]; deeper[cls==2]=B[2]; deeper[cls==3]=B[3]
    spark=((X.astype(int)+Y.astype(int)*3)%11==0)&(sn>.82)
    img[c&~spark]=deeper[c&~spark]; img[c&spark]=B3
panels=[]
for name,fn in [('slonce',lambda i,c: sun(3,i,c)),('deszcz',lambda i,c: rain(5.3,i,c)),('ksiezyc',lambda i,c: moon(2,i)),('wiatr',lambda i,c: wind(4,i,c))]:
    img,cls=base(); fn(img,cls); panels.append(img)
sheet=np.concatenate(panels,0).clip(0,255).astype(np.uint8)
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
Image.fromarray(sheet).resize((W*3,H*4*3),Image.NEAREST).save(os.path.join(ROOT, 'docs', 'img', 'woda_efekty.png'))
