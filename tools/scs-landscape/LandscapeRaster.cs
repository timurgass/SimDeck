// SPDX-License-Identifier: GPL-2.0-only
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Original procedural textures, not downloaded imagery or shipped game assets.
// Masks come from the game. Texture details are illustrative, not individual
// tree/building positions. Their world anchor is stable across imported regions.
static class LandscapeRaster
{
    static readonly string[] Kinds=["ground","grass","field","urban","sand","rock","forest","water"];
    public static string Render(Area[] areas,float x,float z,float span,bool day,string style)
    {
        const int size=2048;
        using var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.None;
            var palette=day?new[]{"#c3c8aa","#c2d0a4","#c7b583","#cecbbd","#d8c79c","#9fab9e","#749279","#91bdc7"}
                :new[]{"#303b34","#354e3b","#514b32","#344149","#504b3a","#414b47","#234936","#214c63"};
            for(var kind=0;kind<Kinds.Length;kind++)
            {
                using var tile=Texture(Kinds[kind],day,style);
                using var texture=new TextureBrush(tile,WrapMode.Tile);
                var k=size/span;
                using var transform=new Matrix(k,0,0,k,(span/2-x)*k,(span/2-z)*k);
                texture.Transform=transform;
                using var solid=new SolidBrush(ColorTranslator.FromHtml(palette[kind]));
                foreach(var area in areas.Where(a=>a.Kind==Kinds[kind]))
                {
                    var points=new PointF[area.P.Length/2];
                    for(var i=0;i<points.Length;i++)points[i]=new((area.P[2*i]-x+span/2)*k,(area.P[2*i+1]-z+span/2)*k);
                    g.FillPolygon(style=="satellite"||kind==6?texture:solid,points,FillMode.Winding);
                }
            }
        }
        // Four bits per channel keep textured regions small enough for the
        // existing bounded transfer. Geometry/alpha are not changed.
        if(style=="satellite")
        {
            var data=bitmap.LockBits(new(0,0,size,size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try{var row=new int[size];for(var y=0;y<size;y++){var pointer=data.Scan0+y*data.Stride;Marshal.Copy(pointer,row,0,size);for(var i=0;i<size;i++)row[i]&=unchecked((int)0xfff0f0f0);Marshal.Copy(row,0,pointer,size);}}
            finally{bitmap.UnlockBits(data);}
        }
        using var stream=new MemoryStream();bitmap.Save(stream,ImageFormat.Png);
        return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());
    }
    static Bitmap Texture(string kind,bool day,string style)
    {
        var image=new Bitmap(256,256,PixelFormat.Format32bppArgb);
        var seed=Array.IndexOf(Kinds,kind)*1789+731;
        var color=kind switch{
            "forest"=>Color.FromArgb(48,76,45),"grass"=>Color.FromArgb(103,117,64),
            "field"=>Color.FromArgb(143,126,78),"water"=>Color.FromArgb(37,82,92),
            "urban"=>Color.FromArgb(118,117,109),"sand"=>Color.FromArgb(173,153,114),
            "rock"=>Color.FromArgb(123,124,111),_=>Color.FromArgb(119,111,84)};
        for(var y=0;y<256;y++)for(var x=0;x<256;x++)
        {
            // Periodic multiscale noise: no hard edges where texture tiles meet.
            var n=Noise(x,y,64,seed)*10+Noise(x,y,16,seed+7)*7+Noise(x,y,4,seed+19)*3;
            if(kind=="field")n+=Math.Sin((x+y*.3)*.85)*7;
            if(kind=="water")n+=Math.Sin((x+y*.6)*.15)*2;
            if(kind=="rock")n+=Math.Sin((x-y*.6)*.09)*6;
            var factor=day?1:.64;
            int C(int v)=>Math.Clamp((int)((v+n)*factor),0,255);
            image.SetPixel(x,y,Color.FromArgb(C(color.R),C(color.G),C(color.B)));
        }
        using var g=Graphics.FromImage(image);g.SmoothingMode=SmoothingMode.AntiAlias;
        var random=new Random(seed);
        if(kind=="forest")
        {
            // Wrap crowns and shadows across all four tile edges.
            for(var i=0;i<520;i++)
            {
                var cx=random.Next(256);var cy=random.Next(256);var r=random.Next(4,10);
                var green=random.Next(61,114);var factor=day?1:.68;
                for(var dx=-256;dx<=256;dx+=256)for(var dy=-256;dy<=256;dy+=256)
                {
                    var px=cx+dx;var py=cy+dy;
                    using var shadow=new SolidBrush(Color.FromArgb(90,9,22,13));
                    g.FillEllipse(shadow,px-r+3,py-r+4,r*2+2,r*2+2);
                    using var shape=new GraphicsPath();shape.AddEllipse(px-r,py-r,r*2,r*2);
                    using var canopy=new PathGradientBrush(shape){CenterPoint=new(px-r*.25f,py-r*.3f),
                        CenterColor=Color.FromArgb((int)(green*.64*factor),(int)(green*factor),(int)(green*.5*factor)),
                        SurroundColors=[Color.FromArgb((int)(green*.24*factor),(int)(green*.48*factor),(int)(green*.23*factor))]};
                    g.FillPath(canopy,shape);
                }
            }
        }
        if(kind=="urban"&&style=="satellite")
        {
            // Representative roof/paving texture only: no invented map objects
            // or navigable roads are added by this pattern.
            for(var y=0;y<256;y+=32)for(var x=0;x<256;x+=32)
            {
                var shade=random.Next(105,159);var f=day?1:.64;
                using var shadow=new SolidBrush(Color.FromArgb(70,20,23,20));g.FillRectangle(shadow,x+9,y+9,18,15);
                using var roof=new SolidBrush(Color.FromArgb((int)(shade*f),(int)((shade-5)*f),(int)((shade-13)*f)));
                g.FillRectangle(roof,x+6,y+6,random.Next(12,21),random.Next(9,17));
            }
        }
        return image;
    }
    static double Noise(int x,int y,int cell,int seed)
    {
        var count=256/cell;var a=x/cell;var b=y/cell;
        double H(int i,int j){unchecked{uint v=(uint)((i%count)*374761393+(j%count)*668265263+seed);v=(v^(v>>13))*1274126177;return (v^(v>>16))/(double)uint.MaxValue*2-1;}}
        var tx=(double)(x%cell)/cell;var ty=(double)(y%cell)/cell;
        tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
        return (H(a,b)*(1-tx)+H(a+1,b)*tx)*(1-ty)+(H(a,b+1)*(1-tx)+H(a+1,b+1)*tx)*ty;
    }
}
