from PIL import Image
import numpy as np, sys, os

def stat(path, label):
    im = Image.open(path).convert('RGB')
    a = np.asarray(im).astype(np.float32)/255.0
    # sRGB-weighted luminance (프로젝트가 보고에 써 온 규약과 동일)
    lum = 0.2126*a[...,0] + 0.7152*a[...,1] + 0.0722*a[...,2]
    r,g,b = a[...,0].mean(), a[...,1].mean(), a[...,2].mean()
    print(f"{label:34s} mean={lum.mean():.4f} p50={np.percentile(lum,50):.4f} "
          f"p99={np.percentile(lum,99):.4f} <0.02={100*(lum<0.02).mean():5.2f}% "
          f">0.50={100*(lum>0.50).mean():5.2f}%  g/r={g/max(r,1e-6):.3f} b/r={b/max(r,1e-6):.3f}")

stat("docs/references/elevator/mood_20260804_user_cabin.png", "REFERENCE (사용자 제공)")
print()
for tag,d in [("baseline","Captures/baseline_20260804"),
              ("v3 postON","Captures/cabin_v3_20260804/postON"),
              ("v4 postON","Captures/cabin_v4_20260804")]:
    for name in ["A_entry_to_machine","C_toward_gate","D_wide_corner"]:
        p = os.path.join(d, name+".png")
        if os.path.exists(p): stat(p, f"{tag} {name[:14]}")
    print()
